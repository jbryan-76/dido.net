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

namespace DidoNet.Test.Runner
{
    public class ApiTests : IClassFixture<TestFixture>
    {
        static long NextPort = 9100;

        /// <summary>
        /// Gets a unique port number so multiple client/server tests can run simultaneously.
        /// </summary>
        /// <returns></returns>
        internal static int GetNextAvailablePort()
        {
            return (int)Interlocked.Increment(ref NextPort);
        }

        readonly TestFixture TestFixture;

        public ApiTests(TestFixture fixture, ITestOutputHelper output)
        {
            TestFixture = fixture;
            //var converter = new OutputConverter(output, "OUTPUT.txt");
            //Console.SetOut(converter);
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.RemoteExecuteAsync using a local loopback server.
        /// </summary>
        [Fact]
        public async void RunRemote()
        {
            // create a test lambda expression
            int testArgument = 123;
            var lambda = await CreateTestLambdaAsync(testArgument);

            // compile and execute the lambda to get the expected result and confirm it matches expectations
            var expectedResult = lambda.Compile().Invoke(TestFixture.Environment.ExecutionContext!);
            Assert.Equal(testArgument, expectedResult);

            // create and start a secure localhost loopback runner server that can execute serialized expressions
            var runnerServer = new RunnerServer();
            int port = GetNextAvailablePort();
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

            // create configuration to use the loopback server
            var configuration = new Configuration
            {
                MaxTries = 1,
                RunnerUri = new Uri($"https://localhost:{port}"),
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // execute the lambda expression using the remote runner server
            var result = await Dido.RunAsync<int>(lambda, configuration);

            // confirm the results match
            Assert.Equal(expectedResult, result);

            // cleanup
            runnerServer.Dispose();
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.RemoteExecuteAsync using a local loopback server.
        /// </summary>
        [Fact]
        public async void RunRemoteWithDeferredResultHandling()
        {
            // create a test lambda expression
            int testArgument = 123;
            var lambda = await CreateTestLambdaAsync(testArgument);

            // compile and execute the lambda to get the expected result and confirm it matches expectations
            var expectedResult = lambda.Compile().Invoke(TestFixture.Environment.ExecutionContext!);
            Assert.Equal(testArgument, expectedResult);

            // create and start a secure localhost loopback runner server that can execute serialized expressions
            var runnerServer = new RunnerServer();
            int port = GetNextAvailablePort();
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

            // create configuration to use the loopback server
            var configuration = new Configuration
            {
                MaxTries = 1,
                RunnerUri = new Uri($"https://localhost:{port}"),
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // execute the lambda expression using the remote runner server using the deferred handler
            // (NOTE the handler is run in a different thread)
            var reset = new AutoResetEvent(false);
            Dido.Run(lambda, (result) =>
            {
                // confirm the results match
                Assert.Equal(expectedResult, result);

                // signal the main thread that it can proceed
                reset.Set();
            }, configuration);

            // block here until the result is received
            reset.WaitOne();

            // cleanup
            runnerServer.Dispose();
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

        [Fact]
        public async void RunRemoteWithTimeout()
        {
            // create and start a secure localhost loopback runner server that can execute serialized expressions
            var runnerServer = new RunnerServer();
            int port = GetNextAvailablePort();
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

            // create configuration to use the loopback server and to timeout after a half second
            var configuration = new Configuration
            {
                MaxTries = 1,
                TimeoutInMs = 500,
                RunnerUri = new Uri($"https://localhost:{port}"),
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // execute the busy loop using the remote runner and confirm it throws TimeoutException
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                var result = await Dido.RunAsync<bool>((context) => BusyLoopWithCancellation.DoFakeWork(context), configuration);
            });

            // cleanup
            runnerServer.Dispose();
        }

        [Fact]
        public async void RunRemoteWithCancel()
        {
            // create and start a secure localhost loopback runner server that can execute serialized expressions
            var runnerServer = new RunnerServer();
            int port = GetNextAvailablePort();
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

            // create configuration to use the loopback server
            var configuration = new Configuration
            {
                MaxTries = 1,
                RunnerUri = new Uri($"https://localhost:{port}"),
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // create a cancellation source and timer to cancel the task after 1 second
            using (var source = new CancellationTokenSource())
            using (var timer = new Timer((_) => source.Cancel(), null, 1000, 0))
            {
                // execute the busy loop using the remote runner and confirm it throws OperationCanceledException
                await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    var result = await Dido.RunAsync<bool>(
                        (context) => BusyLoopWithCancellation.DoFakeWork(context),
                        configuration,
                        source.Token);
                });
            }

            // cleanup
            runnerServer.Dispose();
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.RemoteExecuteAsync using local loopback mediator and runner servers.
        /// </summary>
        [Fact]
        public async void RunRemoteWithMediator()
        {
            // create a test lambda expression
            int testArgument = 123;
            var lambda = await CreateTestLambdaAsync(testArgument);

            // compile and execute the lambda to get the expected result and confirm it matches expectations
            var expectedResult = lambda.Compile().Invoke(TestFixture.Environment.ExecutionContext!);
            Assert.Equal(testArgument, expectedResult);

            // create and start a secure localhost loopback mediator server that can orchestrate runners
            var mediatorServer = new MediatorServer();
            int mediatorPort = GetNextAvailablePort();
            mediatorServer.Start(TestSelfSignedCert.ServerCertificate, mediatorPort, IPAddress.Loopback);

            // create and start a secure localhost loopback runner server that registers to the mediator
            var runnerPort = GetNextAvailablePort();
            var runnerServer = new RunnerServer(new RunnerConfiguration
            {
                //Endpoint = $"https://localhost:{runnerPort}",
                Endpoint = new UriBuilder("https", "localhost", runnerPort).Uri.ToString(),
                //MediatorUri = $"https://localhost:{mediatorPort}",
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
                //MediatorUri = new Uri($"https://localhost:{mediatorPort}"),
                MediatorUri = new UriBuilder("https", "localhost", mediatorPort).Uri,
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // execute the lambda expression using the mediator to choose the runner
            var result = await Dido.RunAsync<int>(lambda, configuration);

            // confirm the results match
            Assert.Equal(expectedResult, result);

            // cleanup
            runnerServer.Dispose();
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
            // and are instead resolved dynamically on demand as needed by the remote runner)
            var context = new AssemblyLoadContext(Guid.NewGuid().ToString(), true);
            var testLibStream = await TestFixture.Configuration.ResolveLocalAssemblyAsync("Dido.TestLib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var testLibAssembly = context.LoadFromStream(testLibStream);
            testLibStream.Dispose();
            var testLibDependencyStream = await TestFixture.Configuration.ResolveLocalAssemblyAsync("Dido.TestLibDependency, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var testLibDependencyAssembly = context.LoadFromStream(testLibDependencyStream);
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