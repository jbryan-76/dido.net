﻿using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET
{
    // NOTE: it's outside the scope of this library to decide how and when to spin up new Runners.

    public class RunnerServer : IDisposable
    {
        private long IsRunning = 0;

        private Thread? WorkLoopThread = null;

        private Connection? OrchestratorConnection = null;

        private MessageChannel? OrchestratorChannel = null;

        private RunnerConfiguration Configuration;

        private bool IsDisposed = false;

        private ConcurrentDictionary<Guid, Worker> ActiveWorkers = new ConcurrentDictionary<Guid, Worker>();

        private ConcurrentQueue<Worker> CompletedWorkers = new ConcurrentQueue<Worker>();

        public RunnerServer(RunnerConfiguration? configuration = null)
        {
            Configuration = configuration ?? new RunnerConfiguration();
        }

        public void Dispose()
        {
            Stop();
            if (!IsDisposed)
            {
                OrchestratorConnection?.Dispose();
                IsDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        public async Task Start(X509Certificate2 cert, int port, IPAddress? ip = null)
        {
            ip = ip ?? IPAddress.Any;

            // update the configured endpoint applications should use to connect,
            // if one was not provided
            if (Configuration.Endpoint == null)
            {
                Configuration.Endpoint = new UriBuilder("https", ip.ToString(), port).Uri;
            }

            if (Configuration.OrchestratorUri != null)
            {
                // create a secure connection to the optional orchestrator
                var uri = Configuration.OrchestratorUri;
                var client = new TcpClient(uri!.Host, uri.Port);
                OrchestratorConnection = new Connection(client, uri.Host);
                OrchestratorChannel = new MessageChannel(OrchestratorConnection, Constants.RunnerChannelNumber);

                // announce this runner to the orchestrator
                SendOrchestratorMessage(new RunnerStartMessage(
                    Configuration.Endpoint.ToString(), Configuration.MaxTasks,
                    Configuration.MaxQueue, Configuration.Label, Configuration.Tags));
                SendOrchestratorMessage(new RunnerStatusMessage(RunnerStatusMessage.States.Starting, 0, 0));

                // TODO: start an infrequent (eg 60s) heartbeat to orchestrator to update status and environment stats
            }

            // listen for incoming connections
            var listener = new TcpListener(ip, port);
            listener.Start();

            // start a work thread to accept and process new connections
            IsRunning = 1;
            WorkLoopThread = new Thread(() => WorkLoop(listener, cert));
            WorkLoopThread.Start();
        }

        public void Stop()
        {
            // inform the orchestrator that this runner is stopping
            SendOrchestratorMessage(new RunnerStatusMessage(RunnerStatusMessage.States.Stopping, 0, 0));

            // signal the work thread to stop
            Interlocked.Exchange(ref IsRunning, 0);

            // wait for it to finish
            WorkLoopThread?.Join();
            WorkLoopThread = null;
        }

        private void SendOrchestratorMessage(IMessage message)
        {
            if (OrchestratorChannel != null)
            {
                OrchestratorChannel.Send(message);
            }
        }

        private void WorkLoop(TcpListener listener, X509Certificate2 cert)
        {
            while (Interlocked.Read(ref IsRunning) == 1)
            {
                CleanupCompletedWorkers();

                // TODO: how to handle serial queued vs parallel requests

                // sleep unless a new connection is pending
                if (!listener.Pending())
                {
                    ThreadHelpers.Yield();
                    continue;
                }

                // block and wait for the next incoming connection
                var client = listener.AcceptTcpClient();

                // create a secure connection to the endpoint
                var connection = new Connection(client, cert);

                // the orchestrator uses an optimistic scheduling strategy, which means
                // it will route traffic to runners based on the conditions known at the 
                // time of the decision, which may differ from the runner conditions by
                // the time the application connects. 
                // if this runner is "too busy" because it already has the maximum
                // number of tasks running, send a "too busy" message, and disconnect.
                // this will (probably) cause the application to try again, up to
                // its configured level of patience.
                if (ActiveWorkers.Count >= Configuration.MaxTasks)
                {
                    //var expressionChannel = connection.GetChannel(Constants.TaskChannel);

                }

                // create and add a worker to track the connection...
                var worker = new Worker()
                {
                    Connection = connection
                };
                ActiveWorkers.TryAdd(worker.Id, worker);

                // ...and start processing it in a dedicated thread
                worker.Thread = new Thread(() => ProcessClient(worker));
                worker.Thread.Start();
            }

            CleanupCompletedWorkers();
        }

        private void CleanupCompletedWorkers()
        {
            while (CompletedWorkers.TryDequeue(out Worker? worker))
            {
                worker?.Thread.Join(1000);
                worker?.Connection.Dispose();
            }
        }

        private async void ProcessClient(Worker worker)
        {
            // create communication channels to the application for: tasks, assemblies, files
            var tasksChannel = new MessageChannel(worker.Connection, Constants.TaskChannelNumber);
            var assembliesChannel = worker.Connection.GetChannel(Constants.AssemblyChannelNumber);
            var filesChannel = worker.Connection.GetChannel(Constants.FileChannelNumber);

            try
            {
                if (ActiveWorkers.Count >= Configuration.MaxTasks)
                {
                    // TODO: if this runner is "too busy" send a "too busy" message
                }

                // TODO: signal any orchestrator that we're starting

                // TODO: add support for the application to send a "cancel" message

                // create the execution context that is available to the expression while it's running
                var executionContext = new ExecutionContext
                {
                    ExecutionMode = ExecutionModes.Local,
                    FilesChannel = filesChannel,
                };

                // create the runtime environment
                var environment = new Environment
                {
                    AssemblyContext = new AssemblyLoadContext(Guid.NewGuid().ToString(), true),
                    ExecutionContext = executionContext,
                    ResolveRemoteAssemblyAsync = new DefaultRemoteAssemblyResolver(assembliesChannel).ResolveAssembly,
                };

                // TODO: create cancellation token

                var reset = new AutoResetEvent(false);
                tasksChannel.OnMessageReceived += async (message, channel) =>
                {
                    try
                    {
                        switch (message)
                        {
                            case TaskRequestMessage expressionRequest:
                                using (var stream = new MemoryStream(expressionRequest.Bytes))
                                {
                                    // TODO: execute in a task.Run so it can be cancelled or timed out

                                    Func<ExecutionContext, object> decodedExpression = null;

                                    try
                                    {
                                        // deserialize the expression
                                        decodedExpression = await ExpressionSerializer.DeserializeAsync<object>(stream, environment);
                                    }
                                    catch (Exception ex)
                                    {
                                        // catch and report deserialization errors
                                        var errorMessage = new TaskErrorMessage(ex, TaskErrorMessage.ErrorTypes.Deserialization);
                                        tasksChannel.Send(errorMessage);
                                    }

                                    try
                                    {
                                        // execute it
                                        var result = decodedExpression?.Invoke(environment.ExecutionContext);

                                        // send the result back to the application on the expression channel
                                        var resultMessage = new TaskResponseMessage(result);
                                        tasksChannel.Send(resultMessage);
                                    }
                                    catch (Exception ex)
                                    {
                                        // catch and report invokation errors
                                        var errorMessage = new TaskErrorMessage(ex, TaskErrorMessage.ErrorTypes.Invokation);
                                        tasksChannel.Send(errorMessage);
                                    }

                                    reset.Set();
                                }
                                break;
                            default:
                                throw new InvalidOperationException($"Message {message.GetType()} is unknown");
                        }
                    }
                    catch (Exception ex)
                    {
                        // all exceptions must be handled explicitly since this handler is running in another
                        // thread that is never awaited
                        var errorMessage = new TaskErrorMessage(ex, TaskErrorMessage.ErrorTypes.General);
                        tasksChannel.Send(errorMessage);
                        reset.Set();
                    }
                };
                reset.WaitOne();
            }
            catch (Exception ex)
            {
                // send the exception back to the application on the expression channel
                var errorMessage = new TaskErrorMessage(ex, TaskErrorMessage.ErrorTypes.General);
                tasksChannel.Send(errorMessage);
                // TODO: log it?
            }
            finally
            {
                // move the worker to the completed queue so it can
                // be disposed by the main thread
                ActiveWorkers.Remove(worker.Id, out _);
                CompletedWorkers.Enqueue(worker);

                // TODO: signal any orchestrator that we're finished
            }
        }

        private class Worker
        {
            public Guid Id { get; private set; } = Guid.NewGuid();
            public Thread Thread;
            public Connection Connection;
        }
    }
}