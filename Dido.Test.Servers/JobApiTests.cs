using DidoNet.Test.Common;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DidoNet.Test.Servers
{
    public class JobApiTests : IClassFixture<TestFixture>
    {
        static long NextPort = 9200;

        /// <summary>
        /// Gets a unique port number so multiple client/server tests can run simultaneously.
        /// </summary>
        /// <returns></returns>
        internal static int GetNextAvailablePort()
        {
            return (int)Interlocked.Increment(ref NextPort);
        }

        readonly TestFixture TestFixture;

        public JobApiTests(TestFixture fixture, ITestOutputHelper output)
        {
            TestFixture = fixture;
            //var converter = new OutputConverter(output, "OUTPUT.txt");
            //Console.SetOut(converter);
        }

        static class BusyLoopWithCancellation
        {
            public static bool DoFakeWork(ExecutionContext context)
            {
                while (!context.Cancel.IsCancellationRequested)
                {
                    Thread.Sleep(1);
                }
                return true;
            }
        }

        static class SleepThenException
        {
            public static bool DoFakeWork(string message)
            {
                Thread.Sleep(250);
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.SubmitJobAsync using local loop-back mediator and runner servers.
        /// </summary>
        [Fact]
        public async void RunJob()
        {
            // create a test lambda expression
            int testArgument = 123;
            var lambda = await CreateTestLambdaAsync(testArgument);

            // compile and execute the lambda to get the expected result and confirm it matches expectations
            var expectedResult = lambda.Compile().Invoke(TestFixture.Environment.ExecutionContext!);
            Assert.Equal(testArgument, expectedResult);

            // create and start a secure localhost loop-back mediator server that can orchestrate runners
            var mediatorServer = new MediatorServer();
            int mediatorPort = GetNextAvailablePort();
            mediatorServer.Start(TestSelfSignedCert.ServerCertificate, mediatorPort, IPAddress.Loopback);

            // create and start a secure localhost loop-back runner server that registers to the mediator
            var runnerPort = GetNextAvailablePort();
            var runnerServer = new RunnerServer(new RunnerConfiguration
            {
                Endpoint = new UriBuilder("https", "localhost", runnerPort).Uri.ToString(),
                MediatorUri = new UriBuilder("https", "localhost", mediatorPort).Uri.ToString(),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            });
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, runnerPort, IPAddress.Loopback);

            // wait for the runner to reach a "ready" state in the mediator
            while (mediatorServer.RunnerPool.Count == 0 || mediatorServer.RunnerPool.First().State != RunnerStates.Ready)
            {
                Thread.Sleep(1);
            }

            // create a configuration to use the mediator to locate a runner to execute the task
            var configuration = new Configuration
            {
                MaxTries = 1,
                MediatorUri = new UriBuilder("https", "localhost", mediatorPort).Uri,
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName, out _),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // execute the lambda expression using the jobs interface
            var jobHandle = await Dido.SubmitJobAsync<int>(lambda, configuration);

            // check on the job periodically until it completes
            JobResult<int> result;
            do
            {
                Thread.Sleep(1);
                result = await Dido.QueryJobAsync<int>(jobHandle, configuration);
            } while (result.Status == JobStatus.Running);

            // confirm the results match
            Assert.Equal(JobStatus.Complete, result.Status);
            Assert.Equal(expectedResult, result.Result);

            // cleanup
            await Dido.DeleteJobAsync(jobHandle, configuration);
            jobHandle.Dispose();
            runnerServer.DeleteCache();
            runnerServer.Dispose();
            mediatorServer.Dispose();
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.SubmitJobAsync using local loop-back mediator and runner servers
        /// and a expression that throws during execution.
        /// </summary>
        [Fact]
        public async void RunJobWithException()
        {
            // create and start a secure localhost loop-back mediator server that can orchestrate runners
            var mediatorServer = new MediatorServer();
            int mediatorPort = GetNextAvailablePort();
            mediatorServer.Start(TestSelfSignedCert.ServerCertificate, mediatorPort, IPAddress.Loopback);

            // create and start a secure localhost loop-back runner server that registers to the mediator
            var runnerPort = GetNextAvailablePort();
            var runnerServer = new RunnerServer(new RunnerConfiguration
            {
                Endpoint = new UriBuilder("https", "localhost", runnerPort).Uri.ToString(),
                MediatorUri = new UriBuilder("https", "localhost", mediatorPort).Uri.ToString(),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            });
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, runnerPort, IPAddress.Loopback);

            // wait for the runner to reach a "ready" state in the mediator
            while (mediatorServer.RunnerPool.Count == 0 || mediatorServer.RunnerPool.First().State != RunnerStates.Ready)
            {
                Thread.Sleep(1);
            }

            // create a configuration to use the mediator to locate a runner to execute the task
            var configuration = new Configuration
            {
                MaxTries = 1,
                MediatorUri = new UriBuilder("https", "localhost", mediatorPort).Uri,
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName, out _),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // execute the fake work expression using the jobs interface
            var message = Guid.NewGuid().ToString();
            var jobHandle = await Dido.SubmitJobAsync<bool>(
                (context) => SleepThenException.DoFakeWork(message),
                configuration);

            // check on the job periodically until it completes
            JobResult<int> result;
            do
            {
                Thread.Sleep(1);
                result = await Dido.QueryJobAsync<int>(jobHandle, configuration);
            } while (result.Status == JobStatus.Running);

            // confirm the job failed with an exception
            Assert.Equal(JobStatus.Error, result.Status);
            Assert.NotNull(result.Exception);

            // cleanup
            await Dido.DeleteJobAsync(jobHandle, configuration);
            jobHandle.Dispose();
            runnerServer.DeleteCache();
            runnerServer.Dispose();
            mediatorServer.Dispose();
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.SubmitJobAsync using local loop-back mediator and runner servers
        /// and a expression that throws during execution.
        /// </summary>
        [Fact]
        public async void RunJobWithCancel()
        {
            // create and start a secure localhost loop-back mediator server that can orchestrate runners
            var mediatorServer = new MediatorServer();
            int mediatorPort = GetNextAvailablePort();
            mediatorServer.Start(TestSelfSignedCert.ServerCertificate, mediatorPort, IPAddress.Loopback);

            // create and start a secure localhost loop-back runner server that registers to the mediator
            var runnerPort = GetNextAvailablePort();
            var runnerServer = new RunnerServer(new RunnerConfiguration
            {
                Endpoint = new UriBuilder("https", "localhost", runnerPort).Uri.ToString(),
                MediatorUri = new UriBuilder("https", "localhost", mediatorPort).Uri.ToString(),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            });
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, runnerPort, IPAddress.Loopback);

            // wait for the runner to reach a "ready" state in the mediator
            while (mediatorServer.RunnerPool.Count == 0 || mediatorServer.RunnerPool.First().State != RunnerStates.Ready)
            {
                Thread.Sleep(1);
            }

            // create a configuration to use the mediator to locate a runner to execute the task
            var configuration = new Configuration
            {
                MaxTries = 1,
                MediatorUri = new UriBuilder("https", "localhost", mediatorPort).Uri,
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName, out _),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // execute the fake work expression using the jobs interface
            var message = Guid.NewGuid().ToString();
            var jobHandle = await Dido.SubmitJobAsync<bool>(
                (context) => BusyLoopWithCancellation.DoFakeWork(context),
                configuration);

            // give the job a moment to spin up, then cancel it
            Thread.Sleep(500);
            await Dido.CancelJobAsync(jobHandle, configuration);

            // check on the job periodically until it completes
            JobResult<int> result;
            do
            {
                Thread.Sleep(1);
                result = await Dido.QueryJobAsync<int>(jobHandle, configuration);
            } while (result.Status == JobStatus.Running);

            // confirm the job was cancelled
            Assert.Equal(JobStatus.Cancelled, result.Status);

            // cleanup
            await Dido.DeleteJobAsync(jobHandle, configuration);
            jobHandle.Dispose();
            runnerServer.DeleteCache();
            runnerServer.Dispose();
            mediatorServer.Dispose();
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.SubmitJobAsync using local loop-back mediator and runner servers
        /// and an infinite loop expression and a configuration to timeout the job.
        /// </summary>
        [Fact]
        public async void RunJobWithTimeout()
        {
            // create and start a secure localhost loop-back mediator server that can orchestrate runners
            var mediatorServer = new MediatorServer();
            int mediatorPort = GetNextAvailablePort();
            mediatorServer.Start(TestSelfSignedCert.ServerCertificate, mediatorPort, IPAddress.Loopback);

            // create and start a secure localhost loop-back runner server that registers to the mediator
            var runnerPort = GetNextAvailablePort();
            var runnerServer = new RunnerServer(new RunnerConfiguration
            {
                Endpoint = new UriBuilder("https", "localhost", runnerPort).Uri.ToString(),
                MediatorUri = new UriBuilder("https", "localhost", mediatorPort).Uri.ToString(),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            });
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, runnerPort, IPAddress.Loopback);

            // wait for the runner to reach a "ready" state in the mediator
            while (mediatorServer.RunnerPool.Count == 0 || mediatorServer.RunnerPool.First().State != RunnerStates.Ready)
            {
                Thread.Sleep(1);
            }

            // create a configuration to use the mediator to locate a runner to execute the task
            // and to timeout after a half second
            var configuration = new Configuration
            {
                MaxTries = 1,
                MediatorUri = new UriBuilder("https", "localhost", mediatorPort).Uri,
                ExecutionMode = ExecutionModes.Remote,
                TimeoutInMs = 500,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName, out _),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // execute the fake work expression using the jobs interface
            var message = Guid.NewGuid().ToString();
            var jobHandle = await Dido.SubmitJobAsync<bool>(
                (context) => BusyLoopWithCancellation.DoFakeWork(context),
                configuration);

            // check on the job periodically until it completes
            JobResult<int> result;
            do
            {
                Thread.Sleep(1);
                result = await Dido.QueryJobAsync<int>(jobHandle, configuration);
            } while (result.Status == JobStatus.Running);

            // confirm the job timed out
            Assert.Equal(JobStatus.Timeout, result.Status);

            // cleanup
            await Dido.DeleteJobAsync(jobHandle, configuration);
            jobHandle.Dispose();
            runnerServer.DeleteCache();
            runnerServer.Dispose();
            mediatorServer.Dispose();
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.SubmitJobAsync using local loop-back mediator and runner servers,
        /// then kills the runner to confirm the job is abandoned.
        /// </summary>
        [Fact]
        public async void RunJobWithAbandonment()
        {
            // create and start a secure localhost loop-back mediator server that can orchestrate runners
            var mediatorServer = new MediatorServer();
            int mediatorPort = GetNextAvailablePort();
            mediatorServer.Start(TestSelfSignedCert.ServerCertificate, mediatorPort, IPAddress.Loopback);

            // create and start a secure localhost loop-back runner server that registers to the mediator
            var runnerPort = GetNextAvailablePort();
            var runnerServer = new RunnerServer(new RunnerConfiguration
            {
                Endpoint = new UriBuilder("https", "localhost", runnerPort).Uri.ToString(),
                MediatorUri = new UriBuilder("https", "localhost", mediatorPort).Uri.ToString(),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            });
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, runnerPort, IPAddress.Loopback);

            // wait for the runner to reach a "ready" state in the mediator
            while (mediatorServer.RunnerPool.Count == 0 || mediatorServer.RunnerPool.First().State != RunnerStates.Ready)
            {
                Thread.Sleep(1);
            }

            // create a configuration to use the mediator to locate a runner to execute the task
            var configuration = new Configuration
            {
                MaxTries = 1,
                MediatorUri = new UriBuilder("https", "localhost", mediatorPort).Uri,
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName, out _),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // execute the fake work expression using the jobs interface
            var message = Guid.NewGuid().ToString();
            var jobHandle = await Dido.SubmitJobAsync<bool>(
                (context) => BusyLoopWithCancellation.DoFakeWork(context),
                configuration);

            // give the job a moment to spin up, then kill the runner
            Thread.Sleep(500);
            runnerServer.DeleteCache();
            runnerServer.Dispose();

            // check on the job periodically until it completes
            JobResult<int> result;
            do
            {
                Thread.Sleep(1);
                result = await Dido.QueryJobAsync<int>(jobHandle, configuration);
            } while (result.Status == JobStatus.Running);

            // confirm the job was abandoned
            Assert.Equal(JobStatus.Abandoned, result.Status);

            // cleanup
            await Dido.DeleteJobAsync(jobHandle, configuration);
            jobHandle.Dispose();
            mediatorServer.Dispose();
        }

        /// <summary>
        /// Creates a sample lambda expression to be used in end-to-end execution tests.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="testArgument"></param>
        /// <returns></returns>
        private async Task<Expression<Func<ExecutionContext, Tprop>>> CreateTestLambdaAsync<Tprop>(Tprop testArgument)
        {
            // manually construct a lambda expression using the techniques described in
            // the other CreateLambdaFromDynamicLoadedAssembly unit tests in the IsolatedAssembliesTests class,
            // using a temporary assembly load context to hold the needed assemblies.
            // (NOTE the reason for creating the expression this way is to ensure the assemblies
            // it uses are not automatically included as packages within the unit test project,
            // and are instead resolved dynamically on demand at runtime as needed by the remote runner)
            var context = new AssemblyLoadContext(Guid.NewGuid().ToString(), true);
            var testLibStream = (await TestFixture.Configuration.ResolveLocalAssemblyAsync("Dido.TestLib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"))!;
            var testLibAssembly = context.LoadFromStream(testLibStream);
            testLibStream.Dispose();
            var testLibDependencyStream = (await TestFixture.Configuration.ResolveLocalAssemblyAsync("Dido.TestLibDependency, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"))!;
            _ = context.LoadFromStream(testLibDependencyStream);
            testLibDependencyStream.Dispose();
            var sampleWorkerType = testLibAssembly.GetType("DidoNet.TestLib.SampleWorkerClass");
            var methodInfo = sampleWorkerType!.GetMethod("SimpleMemberMethod")!;
            var testObject = testLibAssembly.CreateInstance("DidoNet.TestLib.SampleWorkerClass");
            var objEx = Expression.Constant(testObject);
            var argEx = Expression.Constant(testArgument);
            var convertEx = Expression.Convert(objEx, sampleWorkerType);
            var bodyEx = Expression.Call(convertEx, methodInfo, argEx);
            var contextParamEx = Expression.Parameter(typeof(ExecutionContext), "context");
            var lambda = Expression.Lambda<Func<ExecutionContext, Tprop>>(bodyEx, contextParamEx);
            context.Unload();

            return lambda;
        }
    }
}