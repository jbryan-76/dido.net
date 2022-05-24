using DidoNet.Test.Common;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DidoNet.Test
{
    public class CertificateTests
    {
        static long NextPort = 8100;

        /// <summary>
        /// Gets a unique port number so multiple client/server tests can run simultaneously.
        /// </summary>
        /// <returns></returns>
        internal static int GetNextAvailablePort()
        {
            return (int)Interlocked.Increment(ref NextPort);
        }

        [Fact]
        public async void BypassCertVerification()
        {
            var port = GetNextAvailablePort();

            // start a local server with the self-signed certificate
            var serverConnectionTask = Task.Run(async () =>
            {
                // listen for incoming connections
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();

                // block and wait for the next incoming connection
                var client = await listener.AcceptTcpClientAsync();

                // create a secure connection to the client
                var serverConnection = new Connection(client, TestSelfSignedCert.ServerCertificate, "server");
                return serverConnection;
            });

            // start a runner that will skip certificate validation and assume the server is authentic
            var clientConnectionTask = Task.Run(() =>
            {
                // connect to the local server
                var client = new TcpClient("localhost", port);

                // return a secure connection to the server
                var settings = new ClientConnectionSettings { ValidaionPolicy = ServerCertificateValidationPolicies._SKIP_ };
                var clientConnection = new Connection(client, "localhost", "client", settings);
                return clientConnection;
            });

            await Task.WhenAll(serverConnectionTask, clientConnectionTask);

            var serverConnection = await serverConnectionTask;
            var clientConnection = await clientConnectionTask;

            // if the client and server successfully connect to each other, the SSL negotiation succeeded
            Assert.NotNull(serverConnection);
            Assert.NotNull(clientConnection);
        }

        [Fact]
        public async void VerifyCertByThumbprint()
        {
            var port = GetNextAvailablePort();
            var thumbprint = TestSelfSignedCert.ServerCertificate.Thumbprint;

            // start a local server with the self-signed certificate
            var serverConnectionTask = Task.Run(async () =>
            {
                // listen for incoming connections
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();

                // block and wait for the next incoming connection
                var client = await listener.AcceptTcpClientAsync();

                // create a secure connection to the client
                var serverConnection = new Connection(client, TestSelfSignedCert.ServerCertificate, "server");
                return serverConnection;
            });

            // start a runner that knows the certificate thumbprint and uses it to verify the server
            var clientConnectionTask = Task.Run(() =>
            {
                // connect to the local server
                var client = new TcpClient("localhost", port);

                // return a secure connection to the server
                var settings = new ClientConnectionSettings
                {
                    ValidaionPolicy = ServerCertificateValidationPolicies.Thumbprint,
                    Thumbprint = thumbprint
                };
                var clientConnection = new Connection(client, "localhost", "client", settings);
                return clientConnection;
            });

            await Task.WhenAll(serverConnectionTask, clientConnectionTask);

            var serverConnection = await serverConnectionTask;
            var clientConnection = await clientConnectionTask;

            // if the client and server successfully connect to each other, the certificate is valid and SSL negotiation succeeded
            Assert.NotNull(serverConnection);
            Assert.NotNull(clientConnection);
        }

        [RunnableInDebugOnly]
        public async void VerifyCertByRootCA()
        {
            // get a self-signed Dido certificate from the machine root CA
            var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var certs = store.Certificates.Find(X509FindType.FindByIssuerName, "Dido", false);
            var didoCert = certs.Cast<X509Certificate2>().FirstOrDefault();
            if (didoCert == null)
            {
                throw new InvalidOperationException("No Dido certificate found in the system root CA. Create and add a self-signed certificate to support this unit test.");
            }

            var port = GetNextAvailablePort();

            // start a local server with the certificate
            var serverConnectionTask = Task.Run(async () =>
            {
                // listen for incoming connections
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();

                // block and wait for the next incoming connection
                var client = await listener.AcceptTcpClientAsync();

                // create a secure connection to the client
                var serverConnection = new Connection(client, didoCert, "server");
                return serverConnection;
            });

            // start a runner that should connect using the certificate
            var clientConnectionTask = Task.Run(() =>
            {
                // connect to the local server
                var client = new TcpClient("localhost", port);

                // return a secure connection to the server
                var settings = new ClientConnectionSettings { ValidaionPolicy = ServerCertificateValidationPolicies.RootCA };
                var clientConnection = new Connection(client, "dido.localhost", "client", settings);
                return clientConnection;
            });

            await Task.WhenAll(serverConnectionTask, clientConnectionTask);

            var serverConnection = await serverConnectionTask;
            var clientConnection = await clientConnectionTask;

            // if the client and server successfully connect to each other, the certificate is valid and SSL negotiation succeeded
            Assert.NotNull(serverConnection);
            Assert.NotNull(clientConnection);
        }
    }
}