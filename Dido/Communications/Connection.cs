using Dido.Utilities;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace DidoNet
{
    /// <summary>
    /// Represents a single bidirectional communications connection between a server and client.
    /// </summary>
    public class Connection : IDisposable
    {
        /// <summary>
        /// Represents a single bi-directional loop-back data stream that can be used to simulate network
        /// connections for testing purposes. The same instance should be used to construct a pair of
        /// Connections, with one designated as a Client and one as a Server.
        /// </summary>
        public class LoopbackProxy : IDisposable
        {
            /// <summary>
            /// Role options when creating a pair of Connections from a single loop-back proxy.
            /// <para/>Note: the designation is effectively arbitrary, but used to ensure each
            /// Connection uses the right "end" of the stream for reading and writing.
            /// </summary>
            public enum Role
            {
                Client,
                Server
            }

            /// <summary>
            /// Provides a unidirectional data stream from a "server" connection to a "client" connection.
            /// </summary>
            internal QueueBufferStream In = new QueueBufferStream { ReadStrategy = QueueBufferStream.ReadStrategies.Block };

            /// <summary>
            /// Provides a unidirectional data stream from a "client" connection to a "server" connection.
            /// </summary>
            internal QueueBufferStream Out = new QueueBufferStream { ReadStrategy = QueueBufferStream.ReadStrategies.Block };

            public void Dispose()
            {
                In.Dispose();
                Out.Dispose();
            }
        }

        /// <summary>
        /// Signature for a method that is invoked when a frame is transmitted or received. 
        /// </summary>
        /// <param name="frame"></param>
        internal delegate void FrameMonitor(Frame frame);

        /// <summary>
        /// How long to wait between sending heartbeat frames.
        /// </summary>
        public static int DefaultHeartbeatPeriodInSeconds = 60; // one minute

        /// <summary>
        /// The optional name for the connection.
        /// </summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>
        /// Indicates whether the connection is connected to the remote endpoint.
        /// </summary>
        public bool IsConnected
        {
            get { return Interlocked.Read(ref Connected) == 1; }
        }

        /// <summary>
        /// Internal handler for unit tests to monitor received frames.
        /// </summary>
        internal FrameMonitor? UnitTestReceiveFrameMonitor;

        /// <summary>
        /// Internal handler for unit tests to monitor transmitted frames.
        /// </summary>
        internal FrameMonitor? UnitTestTransmitFrameMonitor;

        /// <summary>
        /// The underlying connection endpoint.
        /// </summary>
        private readonly TcpClient? EndPoint = null;

        /// <summary>
        /// The underlying secure socket stream.
        /// </summary>
        private readonly SslStream? SecureStream = null;

        /// <summary>
        /// The stream used for reading data from the connection.
        /// </summary>
        private readonly Stream ReadStream;

        /// <summary>
        /// The stream used for writing data to the connection.
        /// </summary>
        private readonly Stream WriteStream;

        /// <summary>
        /// A queue of frames to write to the underlying connection endpoint.
        /// </summary>
        private readonly ConcurrentQueue<Frame> WriteFrameQueue = new ConcurrentQueue<Frame>();

        /// <summary>
        /// A thread responsible for reading data from the underlying connection endpoint.
        /// </summary>
        private Thread? ReadThread;

        /// <summary>
        /// A thread responsible for writing queued data to the underlying connection endpoint.
        /// </summary>
        private Thread? WriteThread;

        /// <summary>
        /// A timer to periodically write a HeartbeatFrame to maintain an active connection.
        /// </summary>
        private Timer? HeartbeatTimer;

        /// <summary>
        /// Any exception thrown by the ReadThread, to be held and re-thrown when the thread joins.
        /// </summary>
        private Exception? ReadThreadException;

        /// <summary>
        /// Any exception thrown by the WriteThread, to be held and re-thrown when the thread joins.
        /// </summary>
        private Exception? WriteThreadException;

        /// <summary>
        /// A queue of data received by the connection that is held until it can be
        /// read and processed as a complete Frame.
        /// </summary>
        private QueueBufferStream ReadBuffer = new QueueBufferStream(false) { ReadStrategy = QueueBufferStream.ReadStrategies.Full };

        /// <summary>
        /// Contains the timestamp of the last data received from the remote connection.
        /// </summary>
        private DateTimeOffset? LastRemoteTraffic;

        /// <summary>
        /// Used with Interlocked to indicate whether the object instance is connected
        /// and the read and write threads should run.
        /// </summary>
        private long Connected = 0;

        /// <summary>
        /// Used with Interlocked to indicate whether the object instance is disconnecting,
        /// to prevent any new channels from being created.
        /// </summary>
        private long IsDisconnecting = 0;

        /// <summary>
        /// Used with Interlocked to indicate whether a heartbeat frame should be sent ASAP.
        /// </summary>
        private long HeartbeatPending = 0;

        /// <summary>
        /// Fulfills IDispose to indicate whether the object is disposed.
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// The heartbeat period the remote end of this connection is using.
        /// (Each side of the connection may have different heartbeat periods).
        /// </summary>
        private int RemoteHeartbeatPeriodInSeconds = DefaultHeartbeatPeriodInSeconds;

        /// <summary>
        /// A thread-safe collection of all Channels using the connection.
        /// </summary>
        private ConcurrentDictionary<ushort, Channel> Channels = new ConcurrentDictionary<ushort, Channel>();

        /// <summary>
        /// The class logger instance.
        /// </summary>
        private ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Create a new secure connection in a server role using the provided endpoint 
        /// and certificate for encryption.
        /// </summary>
        /// <param name="endpoint">The TcpClient connected to the remote client endpoint.</param>
        /// <param name="serverCertificate">The certificate to encrypt the connection.</param>
        /// <param name="name">The optional name for the connection.</param>
        public Connection(TcpClient endpoint, X509Certificate2 serverCertificate, string? name = null)
        {
            Name = name ?? "";
            EndPoint = endpoint;

            // if a certificate is supplied, the connection is implied to be a server
            ReadStream = WriteStream = SecureStream = new SslStream(endpoint.GetStream(), false);

            try
            {
                // Authenticate the server but don't require the client to authenticate.
                SecureStream.AuthenticateAsServer(serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);
                FinishConnecting();
            }
            catch (AuthenticationException e)
            {
                Logger.Info("Authentication failed");
                Logger.Error(e);
                throw;
            }
        }

        /// <summary>
        /// Create a new secure connection in a server role at the provided endpoint (host+port)
        /// and certificate for encryption.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="serverCertificate"></param>
        /// <param name="name"></param>
        public Connection(string host, int port, X509Certificate2 serverCertificate, string? name = null)
            : this(new TcpClient(host, port), serverCertificate, name) { }

        /// <summary>
        /// Create a new secure connection in a client role using the provided endpoint,
        /// which is connected to the provided target host server (for proper and secure encryption,
        /// the target host server must match the server's certificate/credentials).
        /// </summary>
        /// <param name="endpoint">The TcpClient connected to the remote server endpoint.</param>
        /// <param name="targetHost">The host name of the remote server which is used as part of SSL to authenticate the connection.</param>
        /// <param name="name">The optional name for the connection.</param>
        /// <param name="settings">The settings to use when configuring the connection.</param>
        public Connection(TcpClient endpoint, string targetHost, string? name = null, ClientConnectionSettings? settings = null)
        {
            Name = name ?? "";
            EndPoint = endpoint;

            settings = settings ?? new ClientConnectionSettings();

            // otherwise the connection is implied to be a client
            ReadStream = WriteStream = SecureStream = new SslStream(
                EndPoint.GetStream(),
                false,
                settings.ValidaionPolicy == ServerCertificateValidationPolicies._SKIP_
                    ? (RemoteCertificateValidationCallback)BypassRemoteServerCertificateValidation
                    : settings.ValidaionPolicy == ServerCertificateValidationPolicies.RootCA
                    ? (RemoteCertificateValidationCallback)ValidateRemoteServerCertificate
                    : (sender, certificate, chain, sslPolicyErrors) =>
                        ValidateRemoteServerCertificateThumbprint(sender, certificate, chain, sslPolicyErrors, settings.Thumbprint),
                null
                );

            try
            {
                SecureStream.AuthenticateAsClient(targetHost);
                FinishConnecting();
            }
            catch (AuthenticationException e)
            {
                Logger.Info("Authentication failed");
                Logger.Error(e);
                throw;
            }
        }

        /// <summary>
        /// Create a new secure connection in a client role to the provided server endpoint (host+port).
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="name"></param>
        /// <param name="settings">The settings to use when configuring the connection.</param>
        public Connection(string host, int port, string? name = null, ClientConnectionSettings? settings = null)
            : this(new TcpClient(host, port), host, name, settings) { }

        /// <summary>
        /// Create a new loop-back connection that uses the provided proxy instead of a network TcpClient
        /// but otherwise allows testing all communications and data processing.
        /// </summary>
        public Connection(LoopbackProxy proxy, LoopbackProxy.Role role, string? name = null)
        {
            Name = name ?? "";

            ReadStream = role == LoopbackProxy.Role.Client ? proxy.In : proxy.Out;
            WriteStream = role == LoopbackProxy.Role.Client ? proxy.Out : proxy.In;

            FinishConnecting();
        }

        /// <summary>
        /// Cleans up and disposes all managed and unmanaged resources.
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        public void Dispose()
        {
            // cleanly shutdown all channels and processing threads
            Disconnect();

            // dispose any remaining channels
            foreach (var channel in Channels)
            {
                channel.Value.Dispose();
            }

            // cleanup and track any exceptions from threads
            var exceptions = new List<Exception>();
            if (!IsDisposed)
            {
                IsDisposed = true;

                if (ReadThreadException != null)
                {
                    exceptions.Add(ReadThreadException);
                }
                if (WriteThreadException != null)
                {
                    exceptions.Add(WriteThreadException);
                }

                SecureStream?.Dispose();
                ReadBuffer.Dispose();
                HeartbeatTimer?.Dispose();
            }

            GC.SuppressFinalize(this);

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }

        /// <summary>
        /// Indicates whether any channel on the connection has any pending or in-flight data (true)
        /// or is effectively empty (false).
        /// </summary>
        /// <returns></returns>
        public bool InUse()
        {
            return Channels.Any(c => c.Value.InUse);
        }

        /// <summary>
        /// Block and wait for all in-flight data to finish transmitting, 
        /// and all channels to empty of pending data.
        /// <para/>This is useful to call in testing before disposing the connection.
        /// </summary>
        public void WaitWhileInUse()
        {
            while (InUse())
            {
                ThreadHelpers.Yield();
            }
        }

        /// <summary>
        /// Disconnect the remote endpoint and terminate the connection.
        /// </summary>
        public void Disconnect()
        {
            // ensure no new channels will be created by the public API while disconnecting
            Interlocked.Exchange(ref IsDisconnecting, 1);

            // remove and dispose all channels to force them to flush any remaining data
            ushort[] keys;
            while ((keys = Channels.Keys.ToArray()).Length > 0)
            {
                foreach (var key in keys)
                {
                    if (Channels.TryRemove(key, out var channel))
                    {
                        channel.Dispose();
                    }
                }
            }

            // wait for any remaining flushed channel data to be sent
            Flush();

            // signal all threads to stop
            Interlocked.Exchange(ref Connected, 0);

            // wait for the threads to finish
            ReadThread!.Join(1000);
            WriteThread!.Join(1000);

            try
            {
                // gracefully attempt to shut down the other side of the connection by sending a disconnect frame
                WriteFrame(new DisconnectFrame());
            }
            catch (Exception)
            {
                // silently ignore any errors: the disconnect is just to be polite.
                // (if the other side already disconnected, the write will fail)
            }
        }

        /// <summary>
        /// Transmit a debug message to the remote endpoint.
        /// </summary>
        /// <param name="message"></param>
        public void Debug(string message)
        {
            WriteFrameQueue.Enqueue(new DebugFrame(message));
        }

        /// <summary>
        /// Gets or creates a reference to the indicated channel.
        /// <para/>Note: Created channels are disposed implicitly when the owning connection is
        /// disposed, so calling Dispose() on a channel is effectively optional.
        /// </summary>
        /// <param name="channelNumber"></param>
        /// <exception cref="NotConnectedException"></exception>
        /// <returns></returns>
        public Channel GetChannel(ushort channelNumber)
        {
            if (Interlocked.Read(ref Connected) != 1
                || Interlocked.Read(ref IsDisconnecting) == 1)
            {
                throw new NotConnectedException("Can't create channel: not connected.");
            }
            return GetChannelInternal(channelNumber);
        }

        /// <summary>
        /// Removes but DOES NOT DISPOSE the given channel.
        /// </summary>
        /// <param name="channel"></param>
        internal void RemoveChannel(Channel channel)
        {
            Channels.TryRemove(channel.ChannelNumber, out _);
        }

        /// <summary>
        /// Enqueue the given frame for transmission to the remote endpoint.
        /// </summary>
        /// <param name="frame"></param>
        internal void EnqueueFrame(Frame frame)
        {
            WriteFrameQueue.Enqueue(frame);
        }

        /// <summary>
        /// Enqueue the given frames for transmission to the remote endpoint.
        /// </summary>
        /// <param name="frames"></param>
        internal void EnqueueFrames(IEnumerable<Frame> frames)
        {
            foreach (var frame in frames)
            {
                EnqueueFrame(frame);
            }
        }

        /// <summary>
        /// Blocks until all queued frames are transmitted.
        /// <para/>Note if another thread is continually enqueuing frames this method
        /// may not return, so use with care.
        /// </summary>
        internal void Flush()
        {
            // block until all frames are sent
            while (WriteFrameQueue.Count > 0 && IsConnected)
            {
                ThreadHelpers.Yield();
            }
        }

        /// <summary>
        /// Send a heartbeat frame to the remote endpoint.
        /// </summary>
        internal void SendHeartbeat()
        {
            // signal to send a heartbeat frame as soon as possible
            Interlocked.Exchange(ref HeartbeatPending, 1);
        }

        /// <summary>
        /// Finish internal bookkeeping once the remote connection is authenticated.
        /// </summary>
        private void FinishConnecting()
        {
            Interlocked.Exchange(ref Connected, 1);

            if (SecureStream != null)
            {
                // send a heartbeat frame periodically to keep the connection active
                HeartbeatTimer = new Timer((arg) => SendHeartbeat(), null, 0, DefaultHeartbeatPeriodInSeconds * 1000);

                var remote = (IPEndPoint)EndPoint!.Client.RemoteEndPoint!;
                var local = (IPEndPoint)EndPoint!.Client.LocalEndPoint!;
                Logger.Info($"Connection established from {local.Address}:{local.Port} to {remote.Address}:{remote.Port}");
            }
            else
            {
                // log loop-back info
                Logger.Info($"Loop-back connection created.");
            }

            // start separate threads to read and write data
            ReadThread = new Thread(() => ReadLoop());
            ReadThread.Start();
            WriteThread = new Thread(() => WriteLoop());
            WriteThread.Start();

            Interlocked.Exchange(ref IsDisconnecting, 0);
        }

        /// <summary>
        /// Continuously transmits queued frames until the connection is closed.
        /// </summary>
        private void WriteLoop()
        {
            try
            {
                while (Interlocked.Read(ref Connected) == 1)
                {
                    // if a heartbeat is pending, send it immediately
                    if (Interlocked.Read(ref HeartbeatPending) == 1)
                    {
                        var frame = new HeartbeatFrame(DefaultHeartbeatPeriodInSeconds);
                        WriteFrame(frame);
                        Interlocked.Exchange(ref HeartbeatPending, 0);
                    }

                    // send all pending frames
                    while (WriteFrameQueue.TryDequeue(out Frame? frame))
                    {
                        UnitTestTransmitFrameMonitor?.Invoke(frame);
                        WriteFrame(frame);
                    }

                    ThreadHelpers.Yield();
                }
            }
            catch (Exception e)
            {
                WriteThreadException = e;
                // force all threads to terminate on any error
                Interlocked.Exchange(ref Connected, 0);
            }
        }

        /// <summary>
        /// Continuously receives frames until the connection is closed.
        /// </summary>
        private void ReadLoop()
        {
            // TODO: explore async read optimization strategies
            // https://devblogs.microsoft.com/pfxteam/awaiting-socket-operations/

            try
            {
                // set a timeout on the stream so read methods do not block too long.
                // this allows for periodically checking the Connected status to 
                // exit the loop when either this object or the remote side disconnects.
                if (ReadStream.CanTimeout)
                {
                    ReadStream.ReadTimeout = 100;
                }

                // TODO: how big should this buffer be?
                // empirically it appears that 32k is standard for the underlying network classes
                byte[] data = new byte[32 * 1024];
                while (Interlocked.Read(ref Connected) == 1)
                {
                    try
                    {
                        // read as much data as is available and append to the read buffer
                        int read = ReadStream.Read(data, 0, data.Length);
                        if (read > 0)
                        {
                            ReadBuffer.Write(data, 0, read);
                            LastRemoteTraffic = DateTimeOffset.UtcNow;
                        }
                    }
                    catch (IOException e)
                    {
                        // https://stackoverflow.com/questions/37177401/how-to-repeatedly-read-from-net-sslstream-with-a-timeout
                        // if the stream read times out, just ignore:
                        // that is expected if the other end of the connection is not sending any data
                        var socketException = e.InnerException as SocketException;
                        if (socketException != null && socketException.SocketErrorCode == SocketError.TimedOut)
                        {
                            // NOTE it is an anti-pattern to use exception handling to control 
                            // "normal" expected program flow, but in this case there is no way to
                            // cleanly terminate a thread that is blocking indefinitely on
                            // a Stream.Read.
                        }
                        else
                        {
                            throw;
                        }
                    }

                    // if no traffic is seen from the remote side for more than
                    // twice its indicated heartbeat period, force a disconnect
                    TimeSpan remoteIdlePeriod = LastRemoteTraffic.HasValue
                        ? DateTimeOffset.UtcNow - LastRemoteTraffic.Value
                        : TimeSpan.FromSeconds(0);
                    if (remoteIdlePeriod.TotalSeconds > 2 * RemoteHeartbeatPeriodInSeconds)
                    {
                        // signal all threads to terminate
                        Interlocked.Exchange(ref Connected, 0);
                        Logger.Info($"Forcing disconnect: remote connection was idle for more than {2 * RemoteHeartbeatPeriodInSeconds} seconds");
                        break;
                    }

                    // try to read a frame from the read buffer
                    var frame = TryReadFrame();
                    if (frame == null)
                    {
                        continue;
                    }

                    // process the frame
                    if (frame is HeartbeatFrame)
                    {
                        // remember the heartbeat period the remote side is using
                        // to determine if it becomes unresponsive
                        RemoteHeartbeatPeriodInSeconds = (frame as HeartbeatFrame)!.PeriodInSeconds;
                        // NOTE: ignore heartbeats for unit test monitoring
                    }
                    else if (frame is DisconnectFrame)
                    {
                        ThreadHelpers.Debug($"Got disconnect frame. killing threads. writeQ={WriteFrameQueue.Count} readQ={ReadBuffer.Length}");

                        // signal all threads to terminate
                        Interlocked.Exchange(ref Connected, 0);
                        // NOTE: ignore disconnects for unit test monitoring
                    }
                    else if (frame is DebugFrame)
                    {
                        Logger.Debug($"DEBUG: {(frame as DebugFrame)!.Message}");
                        // TODO: add a configurable handler to process the received debug message
                        UnitTestReceiveFrameMonitor?.Invoke(frame);
                    }
                    else if (frame is ChannelDataFrame)
                    {
                        var channel = GetChannelInternal(frame.Channel);
                        channel.Receive(frame.Payload);
                        UnitTestReceiveFrameMonitor?.Invoke(frame);
                    }
                    else
                    {
                        // should be impossible but just in case...
                        throw new InvalidOperationException($"Unknown or unhandled frame received: '{frame.GetType()}'");
                    }
                }
            }
            catch (IOException)
            {
                Logger.Info("Client disconnected - closing the connection.");
                // force all thread termination on any error
                Interlocked.Exchange(ref Connected, 0);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"ReadLoop terminated: Unhandled Exception");
                ReadThreadException = e;
                // force all thread termination on any error
                Interlocked.Exchange(ref Connected, 0);
            }
        }

        /// <summary>
        /// Gets or creates a reference to the indicated channel.
        /// </summary>
        /// <param name="channelNumber"></param>
        /// <returns></returns>
        private Channel GetChannelInternal(ushort channelNumber)
        {
            return Channels.GetOrAdd(channelNumber, (num) => new Channel(this, num));
        }

        /// <summary>
        /// Try to read a frame from the read buffer, returning null if a full frame is not yet
        /// present in the buffer, else the strongly typed frame instance.
        /// </summary>
        /// <returns></returns>
        private Frame? TryReadFrame()
        {
            // remember the current buffer position
            long position = ReadBuffer.Position;

            // try to read a frame from the buffer
            int length = -1;
            var payload = new byte[0];
            if (ReadBuffer.TryReadByte(out var type)
                && ReadBuffer.TryReadUShortBE(out var channel)
                && ReadBuffer.TryReadIntBE(out length)
                && (length == 0 || ReadBuffer.TryReadBytes(length, out payload))
                )
            {
                // a frame was read successfully. clear/consume the buffer up to the current position...
                ReadBuffer.Truncate();
                // ..and return the frame
                var frame = FrameFactory.Decode(new Frame
                {
                    Type = type,
                    Channel = channel,
                    Length = length,
                    Payload = payload!
                });
                ThreadHelpers.Debug($"connection {Name} just read frame {frame}");
                return frame;
            }
            else
            {
                // a new frame is not yet available.
                // restore the buffer position and return null
                //ThreadHelpers.Debug($"connection {Name} no data {ReadBuffer.Position} -> {position} [{ReadBuffer.Length}]");
                ReadBuffer.Position = position;
                return null;
            }
        }

        /// <summary>
        /// Write the provided frame to the underlying SslStream.
        /// </summary>
        /// <param name="frame"></param>
        private void WriteFrame(Frame frame)
        {
            // TODO: use a BufferedStream to increase performance?
            ThreadHelpers.Debug($"connection {Name} writing frame {frame}");
            WriteStream.WriteByte(frame.Type);
            WriteStream.WriteUInt16BE(frame.Channel);
            WriteStream.WriteInt32BE(frame.Length);
            if (frame.Length > 0)
            {
                WriteStream.Write(frame.Payload, 0, frame.Payload.Length);
            }
            WriteStream.Flush();
            ThreadHelpers.Debug($"connection {Name} wrote frame {frame}");
        }

        /// <summary>
        /// Invoked by the RemoteCertificateValidationDelegate to ALWAYS validate the server endpoint,
        /// regardless of its authenticity or validity.
        /// <para/>WARNING: should never be used in production: only for testing and local development.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        private bool BypassRemoteServerCertificateValidation(
              object sender, // SslStream
              X509Certificate? certificate,
              X509Chain? chain,
              SslPolicyErrors sslPolicyErrors)
        {
            Logger.Warn("BYPASSING SERVER CERTIFICATE VALIDATION");
            return true;
        }

        /// <summary>
        /// Invoked by the RemoteCertificateValidationDelegate to authenticate the server endpoint using a common
        /// shared certificate which must be used by the server and stored in the root CA on the client machine.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        private bool ValidateRemoteServerCertificate(
              object sender, // SslStream
              X509Certificate? certificate,
              X509Chain? chain,
              SslPolicyErrors sslPolicyErrors)
        {
            Logger.Info("Validating remote certificate using root CA");

            // TODO: verify chain/certificate against root CA on this machine?

            // NOTE: if the Issuer/Subject of the server cert exactly matches the 'targetHost' parameter and the cert is
            // stored in the client "Trusted Root Certificate Authorities", then sslPolicyErrors will be SslPolicyErrors.None

            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            Logger.Error("Certificate error: {0}", sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }

        /// <summary>
        /// Invoked by the RemoteCertificateValidationDelegate to authenticate the server endpoint when the client
        /// has configured a-priori knowledge of the certificate thumb-print the server will use.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <param name="thumbprint"></param>
        /// <returns></returns>
        private bool ValidateRemoteServerCertificateThumbprint(
              object sender, // SslStream
              X509Certificate? certificate,
              X509Chain? _,
              SslPolicyErrors sslPolicyErrors,
              string thumbprint)
        {
            Logger.Info("Validating remote certificate using thumb-print");

            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            var cert2 = new X509Certificate2(certificate);

            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }
            else if (String.Compare(thumbprint, cert2.Thumbprint, true) == 0)
            {
                return true;
            }

            Logger.Error("Certificate error: {0}", sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }
    }
}