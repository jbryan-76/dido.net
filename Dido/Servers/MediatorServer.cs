using Dido.Utilities;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace DidoNet
{

    internal class MemoryJob : IJob
    {
        public string RunnerId { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public byte[] Data { get; set; } = new byte[0];
    }

    internal class MemoryJobStore : IJobStore
    {
        public Task CreateJob(IJob job)
        {
            lock (Jobs)
            {
                if (Jobs.ContainsKey(job.JobId))
                {
                    throw new Exception($"Duplicate key: A job with id '{job.JobId}' already exists.");
                }
                Jobs.Add(job.JobId, new MemoryJob
                {
                    JobId = job.JobId,
                    RunnerId = job.RunnerId,
                    Status = job.Status,
                    Data = job.Data.ToArray()
                });
            }
            return Task.CompletedTask;
        }

        public Task<bool> UpdateJob(IJob job)
        {
            lock (Jobs)
            {
                if (!Jobs.ContainsKey(job.JobId))
                {
                    return Task.FromResult(false);
                }
                Jobs[job.JobId] = new MemoryJob
                {
                    JobId = job.JobId,
                    RunnerId = job.RunnerId,
                    Status = job.Status,
                    Data = job.Data.ToArray()
                };
            }
            return Task.FromResult(true);
        }

        public Task<bool> SetJobStatus(string jobId, string status)
        {
            lock (Jobs)
            {
                if (!Jobs.ContainsKey(jobId))
                {
                    return Task.FromResult(false);
                }
                Jobs[jobId].Status = status;
            }
            return Task.FromResult(true);
        }

        public Task<IJob?> GetJob(string jobId)
        {
            lock (Jobs)
            {
                Jobs.TryGetValue(jobId, out var job);
                return Task.FromResult((IJob?)job);
            }
        }

        public Task<IEnumerable<IJob>> GetAllJobs(string runnerId)
        {
            lock (Jobs)
            {
                return Task.FromResult(Jobs.Values.Where(j => j.RunnerId == runnerId).Select(j => (IJob)j));
            }
        }

        public Task<bool> DeleteJob(string jobId)
        {
            lock (Jobs)
            {
                return Task.FromResult(Jobs.Remove(jobId));
            }
        }

        private Dictionary<string, MemoryJob> Jobs = new Dictionary<string, MemoryJob>();
    }

    /// <summary>
    /// A configurable Dido Mediator server that can be deployed as a console app, service, or integrated into a
    /// larger application.
    /// </summary>
    public class MediatorServer
    {
        /// <summary>
        /// The current configuration of the mediator.
        /// </summary>
        public MediatorConfiguration Configuration { get; private set; }

        /// <summary>
        /// A backing store for jobs.
        /// </summary>
        public IJobStore JobStore { get; set; } = new MemoryJobStore();

        /// <summary>
        /// The set of known runners.
        /// </summary>
        internal List<RunnerItem> RunnerPool = new List<RunnerItem>();

        /// <summary>
        /// Indicates whether the mediator has started and is running.
        /// </summary>
        private long IsRunning = 0;

        /// <summary>
        /// The thread that accepts and processes new connections.
        /// </summary>
        private Thread? WorkLoopThread;

        /// <summary>
        /// Indicates whether the instance is disposed.
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// The set of all active connected clients;
        /// </summary>
        private ConcurrentDictionary<Guid, ConnectedClient> ActiveClients = new ConcurrentDictionary<Guid, ConnectedClient>();

        /// <summary>
        /// The set of clients that have disconnected and need to be disposed.
        /// </summary>
        private ConcurrentQueue<ConnectedClient> CompletedClients = new ConcurrentQueue<ConnectedClient>();

        /// <summary>
        /// The class logger instance.
        /// </summary>
        private readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Create a new mediator server with the given configuration.
        /// </summary>
        /// <param name="configuration"></param>
        public MediatorServer(MediatorConfiguration? configuration = null)
        {
            Configuration = configuration ?? new MediatorConfiguration();
        }

        public void Dispose()
        {
            Stop();
            if (!IsDisposed)
            {
                IsDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Starts listening for incoming connection requests from applications and runners.
        /// </summary>
        /// <param name="cert"></param>
        /// <param name="port"></param>
        /// <param name="ip"></param>
        public void Start(X509Certificate2 cert, int? port = null, IPAddress? ip = null)
        {
            ip ??= IPAddress.Any;
            port ??= Constants.DefaultPort;

            Logger.Info($"Starting mediator {Configuration.Id} listening at {ip}:{port}");

            // update the configured endpoint applications should use to connect,
            // if one was not provided
            if (Configuration.Endpoint == null)
            {
                var host = ip.ToString();
                if (host == "0.0.0.0")
                {
                    host = "localhost";
                }
                Configuration.Endpoint = new UriBuilder("https", host, port.Value).Uri.ToString();
            }

            Logger.Info($"  Endpoint = {Configuration.Endpoint}");

            // listen for incoming connections
            var listener = new TcpListener(ip, port.Value);
            listener.Start();

            // start a work thread to accept and process new connections
            IsRunning = 1;
            WorkLoopThread = new Thread(() => WorkLoop(listener, cert));
            WorkLoopThread.Start();
        }

        /// <summary>
        /// Stops listening for incoming requests and shuts down the server.
        /// </summary>
        public void Stop()
        {
            Logger.Info($"Stopping mediator {Configuration.Id}...");

            // signal the work thread to stop
            Interlocked.Exchange(ref IsRunning, 0);

            // wait for it to finish
            WorkLoopThread?.Join();
            WorkLoopThread = null;

            // cleanup all clients
            DisconnectAndCleanupActiveClients();
            CleanupCompletedClients();

            Logger.Info($"  Mediator {Configuration.Id} stopped");
        }

        /// <summary>
        /// Finds and returns the next best available runner using the filtering and matching criteria
        /// of the provided request and the configuration and state of all runners in the runner pool.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        internal RunnerItem? GetNextAvailableRunner(RunnerRequestMessage request)
        {
            // if the request identifies a specific runner (by id),
            // find and return that runner immediately, with no filtering
            if (!string.IsNullOrEmpty(request.RunnerId))
            {
                return RunnerPool.FirstOrDefault(x => x.Id == request.RunnerId);
            }

            // initialize a query to the current set of ready runners
            var query = RunnerPool.Where(x => x.State == RunnerStates.Ready);

            // filter to include only matching platforms, if necessary
            if (request.Platforms?.Length > 0)
            {
                query = query.Where(x => request.Platforms.Contains(x.Platform));
            }

            // filter to include only matching runner labels, if necessary
            if (!string.IsNullOrEmpty(request.Label))
            {
                query = query.Where(x => x.Label == request.Label);
            }

            // filter to include only matching runner tags, if necessary
            if (request.Tags?.Length > 0)
            {
                query = query.Where(x => x.Tags.Intersect(request.Tags).Any());
            }

            // filter to include runners that have:
            query = query.Where(x => x.ActiveTasks < x.MaxTasks // available task slots
                || x.MaxQueue < 0 // or unlimited queue
                || x.QueueLength < x.MaxQueue // or available queue
            );

            // now sort them by "most availability":
            // first by number of open slots (ie runners with immediate vacancies)
            query = query.OrderByDescending(x => x.MaxTasks - x.ActiveTasks)
                    // then by shortest queue (ie runners with the least pending work)
                    .ThenBy(x => x.QueueLength);

            // finally materialize and return the first (ie best) matching eligible runner
            lock (RunnerPool)
            {
                return query.FirstOrDefault();
            }
        }

        /// <summary>
        /// The work loop for the main mediator thread, responsible for accepting incoming connections 
        /// from applications that are requesting remote execution of tasks and the runners that are 
        /// processing those tasks.
        /// </summary>
        /// <param name="listener"></param>
        /// <param name="cert"></param>
        private void WorkLoop(TcpListener listener, X509Certificate2 cert)
        {
            while (Interlocked.Read(ref IsRunning) == 1)
            {
                CleanupCompletedClients();

                // sleep unless a new connection is pending
                if (!listener.Pending())
                {
                    ThreadHelpers.Yield();
                    continue;
                }

                // block and wait for the next incoming connection
                var client = listener.AcceptTcpClient();

                // create a secure connection to the endpoint
                // and start processing it in a dedicated thread
                var connection = new Connection(client, cert);
                var id = Guid.NewGuid();
                var thread = new Thread(() => ProcessClient(id, connection));
                thread.Start();

                // track the thread/connection pair as an active client
                ActiveClients.TryAdd(id, new ConnectedClient(thread, connection));
            }
        }

        /// <summary>
        /// Disposes all completed clients.
        /// </summary>
        private void CleanupCompletedClients()
        {
            while (CompletedClients.TryDequeue(out ConnectedClient? client))
            {
                client?.Thread.Join(1000);
                client?.Connection.Dispose();
            }
        }

        /// <summary>
        /// Disconnects and disposes all active clients.
        /// </summary>
        private void DisconnectAndCleanupActiveClients()
        {
            foreach (var client in ActiveClients.Values)
            {
                client.Connection.Disconnect();
                client.Thread.Join(100);
                client.Connection.Dispose();
            }
        }

        /// <summary>
        /// Processes all messages from a runner.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        private async Task RunnerChannelMessageHandler(IMessage message, MessageChannel channel, Connection connection)
        {
            IJob? job = null;
            RunnerItem? runner = null;

            switch (message)
            {
                case RunnerStartMessage start:
                    // TODO: if a runner with the same id already exists, send back error

                    // create and add the runner to the pool, if necessary
                    if (runner == null)
                    {
                        runner = new RunnerItem();
                        lock (RunnerPool)
                        {
                            RunnerPool.Add(runner);
                        }
                    }
                    runner.Init(start, channel);

                    // add a disconnect handler to the connection to mark all in-progress jobs as abandoned
                    connection.OnDisconnect = async (connection, reason) =>
                    {
                        var jobs = await JobStore.GetAllJobs(runner.Id);
                        foreach (var job in jobs)
                        {
                            if (job.Status == JobStatusValues.Running)
                            {
                                _ = JobStore.SetJobStatus(job.JobId, JobStatusValues.Abandoned);
                            }
                        }
                    };

                    // TODO: send back an ack to tell the runner they are ok to continue
                    break;

                case RunnerStatusMessage status:
                    runner = RunnerPool.FirstOrDefault(x => x.Channel == channel);
                    runner?.Update(status);

                    if (runner != null && status.State == RunnerStates.Stopping)
                    {
                        // the runner is stopping:
                        // update the status of all of its in-progress jobs to "abandoned"
                        var jobs = await JobStore.GetAllJobs(runner.Id);
                        foreach (var j in jobs)
                        {
                            if (j.Status == JobStatusValues.Running)
                            {
                                _ = JobStore.SetJobStatus(j.JobId, JobStatusValues.Abandoned);
                            }
                        }
                    }
                    break;

                case JobStartMessage jobStart:
                    await JobStore.CreateJob(new MemoryJob
                    {
                        RunnerId = jobStart.RunnerId,
                        JobId = jobStart.JobId,
                        Status = JobStatusValues.Running
                    });
                    break;

                case JobCompleteMessage jobComplete:
                    var jobStatus = string.Empty;
                    switch (jobComplete.ResultMessage)
                    {
                        case TaskErrorMessage error:
                            jobStatus = JobStatusValues.Error;
                            break;
                        case TaskResponseMessage response:
                            jobStatus = JobStatusValues.Complete;
                            break;
                        case TaskCancelMessage cancel:
                            jobStatus = JobStatusValues.Cancelled;
                            break;
                        case TaskTimeoutMessage timeout:
                            jobStatus = JobStatusValues.Timeout;
                            break;
                    }
                    await JobStore.UpdateJob(new MemoryJob
                    {
                        RunnerId = jobComplete.RunnerId,
                        JobId = jobComplete.JobId,
                        Status = jobStatus,
                        Data = jobComplete.ResultMessageBytes
                    });
                    break;
            }

        }

        /// <summary>
        /// Processes all messages from the application.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        private async Task ApplicationChannelMessageHandler(IMessage message, MessageChannel channel)
        {
            IJob? job = null;
            RunnerItem? runner = null;

            switch (message)
            {
                case RunnerRequestMessage request:
                    runner = GetNextAvailableRunner(request);
                    if (runner == null)
                    {
                        channel.Send(new RunnerNotAvailableMessage());
                    }
                    else
                    {
                        channel.Send(new RunnerResponseMessage(runner.Endpoint));
                    }
                    break;

                case JobQueryMessage jobQuery:
                    job = await JobStore.GetJob(jobQuery.JobId);

                    if (job == null)
                    {
                        channel.Send(new JobNotFoundMessage(jobQuery.JobId));
                    }
                    else
                    {
                        if (job.Status == JobStatusValues.Running)
                        {
                            channel.Send(new JobStartMessage
                            {
                                JobId = job.JobId,
                                RunnerId = job.RunnerId,
                            });
                        }
                        else if (job.Status == JobStatusValues.Abandoned)
                        {
                            channel.Send(new JobCompleteMessage
                            {
                                JobId = job.JobId,
                                RunnerId = job.RunnerId
                            });
                        }
                        else
                        {
                            channel.Send(new JobCompleteMessage
                            {
                                JobId = job.JobId,
                                RunnerId = job.RunnerId,
                                ResultMessageBytes = job.Data
                            });
                        }
                    }
                    break;

                case JobDeleteMessage jobDelete:
                    // find the runner and tell it to cancel the job
                    job = await JobStore.GetJob(jobDelete.JobId);
                    if (job != null && job.Status == JobStatusValues.Running)
                    {
                        runner = RunnerPool.FirstOrDefault(x => x.Id == job.RunnerId);
                        runner?.Channel?.Send(new JobCancelMessage(job.JobId));
                    }

                    // delete the job
                    await JobStore.DeleteJob(jobDelete.JobId);

                    // acknowledge the application
                    channel.Send(new AcknowledgedMessage());
                    break;

                case JobCancelMessage jobCancel:
                    // find the runner and tell it to cancel the job
                    job = await JobStore.GetJob(jobCancel.JobId);
                    if (job != null && job.Status == JobStatusValues.Running)
                    {
                        runner = RunnerPool.FirstOrDefault(x => x.Id == job.RunnerId);
                        runner?.Channel?.Send(new JobCancelMessage(job.JobId));
                    }

                    // acknowledge the application
                    channel.Send(new AcknowledgedMessage());
                    break;
            }
        }

        /// <summary>
        /// Processes the given connection from either an application or a mediator,
        /// handling all received messages and communication.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="connection"></param>
        private void ProcessClient(Guid id, Connection connection)
        {
            // create communication channels.
            // NOTE: by design, exactly one of these channels will receive data
            // depending on whether an application or a runner connected.
            // however, until one of the channels receives data, it's impossible to know which.
            var applicationChannel = new MessageChannel(connection, Constants.MediatorApp_ChannelId);
            var runnerChannel = new MessageChannel(connection, Constants.MediatorRunner_ChannelId);

            try
            {
                // applications will contact the mediator using the application channel
                // to make requests: e.g. get a runner, check job status, etc
                applicationChannel.OnMessageReceived = async (message, channel) =>
                {
                    // this connection is to an application.
                    // close the runner channel so as not to tie up a thread
                    runnerChannel?.Dispose();
                    runnerChannel = null;

                    // process the message
                    await ApplicationChannelMessageHandler(message, channel);
                };

                // runners will contact the mediator using the runner channel to update their status
                runnerChannel.OnMessageReceived = async (message, channel) =>
                {
                    // this connection is to a runner.
                    // close the application channel so as not to tie up a thread
                    applicationChannel?.Dispose();
                    applicationChannel = null;

                    // process the message
                    await RunnerChannelMessageHandler(message, channel, connection);
                };

                // the client will run forever until it disconnects or this server stops
                while (Interlocked.Read(ref IsRunning) == 1 && connection.IsConnected)
                {
                    ThreadHelpers.Yield();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                // if the connection was for a runner, remove it from the pool
                var runner = RunnerPool.FirstOrDefault(x => x.Channel?.Connection == connection);
                if (runner != null)
                {
                    lock (RunnerPool)
                    {
                        RunnerPool.Remove(runner);
                    }
                }

                // find and move the client to the completed queue so it can
                // be disposed by the main thread
                if (ActiveClients.TryGetValue(id, out var client))
                {
                    CompletedClients.Enqueue(client);
                }
            }
        }

        /// <summary>
        /// Tracks a connection from an application or runner, including its local processing thread.
        /// </summary>
        private class ConnectedClient
        {
            /// <summary>
            /// The processing thread for the client.
            /// </summary>
            public Thread Thread { get; private set; }

            /// <summary>
            /// The communications connection for the client.
            /// </summary>
            public Connection Connection { get; private set; }

            /// <summary>
            /// Create a new connected client instance.
            /// </summary>
            /// <param name="thread"></param>
            /// <param name="connection"></param>
            public ConnectedClient(Thread thread, Connection connection)
            {
                Thread = thread;
                Connection = connection;
            }
        }
    }
}