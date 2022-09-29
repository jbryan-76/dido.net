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
    public class RunApiTests : IClassFixture<TestFixture>
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

        public RunApiTests(TestFixture fixture, ITestOutputHelper output)
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
        /// Performs an end-to-end test of Dido.RunAsync using a local loop-back runner server.
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

            // create and start a secure localhost loop-back runner server that can execute serialized expressions
            var runnerServer = new RunnerServer();
            int port = GetNextAvailablePort();
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

            // create configuration to use the loop-back server
            var configuration = new Configuration
            {
                MaxTries = 1,
                RunnerUri = new Uri($"https://localhost:{port}"),
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName, out _),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // execute the lambda expression using the remote runner server
            var result = await Dido.RunAsync<int>(lambda, configuration);

            // confirm the results match
            Assert.Equal(expectedResult, result);

            // cleanup
            runnerServer.DeleteCache();
            runnerServer.Dispose();
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.RunAsync using a local loop-back runner server
        /// configured to cache needed assemblies.
        /// </summary>
        [Fact]
        public async void RunRemoteWithCachedAssemblies()
        {
            var runnerId = Guid.NewGuid().ToString();

            // create a test lambda expression
            int testArgument = 123;
            var lambda = await CreateTestLambdaAsync(testArgument);

            // compile and execute the lambda to get the expected result and confirm it matches expectations
            var expectedResult = lambda.Compile().Invoke(TestFixture.Environment.ExecutionContext!);
            Assert.Equal(testArgument, expectedResult);

            // create and start a secure localhost loop-back runner server that can execute serialized expressions,
            // and assign it a specific id
            using (var runnerServer = new RunnerServer(new RunnerConfiguration
            {
                Id = runnerId
            }))
            {
                int port = GetNextAvailablePort();
                runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

                // create configuration to use the loop-back server
                var configuration = new Configuration
                {
                    MaxTries = 1,
                    RunnerUri = new Uri($"https://localhost:{port}"),
                    ExecutionMode = ExecutionModes.Remote,
                    AssemblyCaching = AssemblyCachingPolicies.Always,
                    // use the unit test assembly resolver instead of the default implementation
                    ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName, out _),
                    // bypass server cert validation since unit tests are using a base-64 self-signed cert
                    ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
                };

                // execute the lambda expression using the remote runner server
                var result = await Dido.RunAsync<int>(lambda, configuration);

                // confirm the results match
                Assert.Equal(expectedResult, result);
            }

            // at this point, the runner cache should contain the assemblies necessary to execute the expression.
            // re-run the expression without the correct remote assembly resolver.
            // the runner should still succeed in executing the expression by loading the cached assemblies
            // (since an incorrect assembly resolver is configured, if the cached assemblies are not loaded an
            // exception *would* be thrown, but *should not* be in this test).

            using (var runnerServer = new RunnerServer(new RunnerConfiguration
            {
                // use the same id so the cache path is the same as the previous runner
                Id = runnerId,
            }))
            {
                int port = GetNextAvailablePort();
                runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

                // create configuration to use the loop-back server
                var configuration = new Configuration
                {
                    MaxTries = 1,
                    RunnerUri = new Uri($"https://localhost:{port}"),
                    ExecutionMode = ExecutionModes.Remote,
                    AssemblyCaching = AssemblyCachingPolicies.Always,
                    // use an intentionally broken resolver:
                    // if the runner properly resolves assemblies using the cache, this delegate should never be invoked
                    ResolveLocalAssemblyAsync = (assemblyName) => throw new NotImplementedException(),
                    // bypass server cert validation since unit tests are using a base-64 self-signed cert
                    ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
                };

                // execute the lambda expression using the remote runner server
                var result = await Dido.RunAsync<int>(lambda, configuration);

                // confirm the results match
                Assert.Equal(expectedResult, result);

                // cleanup
                runnerServer.DeleteCache();
            }
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.RunAsync using a local loop-back runner server
        /// configured to cache and encrypt needed assemblies.
        /// </summary>
        [Fact]
        public async void RunRemoteWithEncryptedCachedAssemblies()
        {
            var runnerId = Guid.NewGuid().ToString();

            // create a test lambda expression
            int testArgument = 123;
            var lambda = await CreateTestLambdaAsync(testArgument);

            // compile and execute the lambda to get the expected result and confirm it matches expectations
            var expectedResult = lambda.Compile().Invoke(TestFixture.Environment.ExecutionContext!);
            Assert.Equal(testArgument, expectedResult);

            // create and start a secure localhost loop-back runner server that can execute serialized expressions,
            // and assign it a specific id
            using (var runnerServer = new RunnerServer(new RunnerConfiguration
            {
                Id = runnerId
            }))
            {
                int port = GetNextAvailablePort();
                runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

                // create configuration to use the loop-back server
                var configuration = new Configuration
                {
                    MaxTries = 1,
                    RunnerUri = new Uri($"https://localhost:{port}"),
                    ExecutionMode = ExecutionModes.Remote,
                    AssemblyCaching = AssemblyCachingPolicies.Always,
                    CachedAssemblyEncryptionKey = runnerId,
                    // use the unit test assembly resolver instead of the default implementation
                    ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName, out _),
                    // bypass server cert validation since unit tests are using a base-64 self-signed cert
                    ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
                };

                // execute the lambda expression using the remote runner server
                var result = await Dido.RunAsync<int>(lambda, configuration);

                // confirm the results match
                Assert.Equal(expectedResult, result);
            }

            // at this point, the runner cache should contain the encrypted assemblies necessary to execute the expression.
            // re-run the expression without the correct remote assembly resolver.
            // the runner should still succeed in executing the expression by loading the cached assemblies
            // (since an incorrect assembly resolver is configured, if the cached assemblies are not loaded an
            // exception *would* be thrown, but *should not* be in this test).

            using (var runnerServer = new RunnerServer(new RunnerConfiguration
            {
                // use the same id so the cache path is the same as the previous runner
                Id = runnerId,
            }))
            {
                int port = GetNextAvailablePort();
                runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

                // create configuration to use the loop-back server
                var configuration = new Configuration
                {
                    MaxTries = 1,
                    RunnerUri = new Uri($"https://localhost:{port}"),
                    ExecutionMode = ExecutionModes.Remote,
                    AssemblyCaching = AssemblyCachingPolicies.Always,
                    CachedAssemblyEncryptionKey = runnerId,
                    // use an intentionally broken resolver:
                    // if the runner properly resolves assemblies using the cache, this delegate should never be invoked
                    ResolveLocalAssemblyAsync = (assemblyName) => throw new NotImplementedException(),
                    // bypass server cert validation since unit tests are using a base-64 self-signed cert
                    ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
                };

                // execute the lambda expression using the remote runner server
                var result = await Dido.RunAsync<int>(lambda, configuration);

                // confirm the results match
                Assert.Equal(expectedResult, result);

                // cleanup
                runnerServer.DeleteCache();
            }
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.Run using a local loop-back runner server,
        /// which invokes a handler when the execution completes, instead of awaiting a task.
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

            // create and start a secure localhost loop-back runner server that can execute serialized expressions
            var runnerServer = new RunnerServer();
            int port = GetNextAvailablePort();
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

            // create configuration to use the loop-back server
            var configuration = new Configuration
            {
                MaxTries = 1,
                RunnerUri = new Uri($"https://localhost:{port}"),
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default assemblyName, out _
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName, out _),
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
            reset.Dispose();

            // cleanup
            runnerServer.DeleteCache();
            runnerServer.Dispose();
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.RunAsync using a local loop-back runner server
        /// and an infinite-loop expression to confirm a timeout exception is thrown.
        /// </summary>
        [Fact]
        public async void RunRemoteWithTimeout()
        {
            // create and start a secure localhost loop-back runner server that can execute serialized expressions
            var runnerServer = new RunnerServer();
            int port = GetNextAvailablePort();
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

            // create configuration to use the loop-back server and to timeout after a half second
            var configuration = new Configuration
            {
                MaxTries = 1,
                TimeoutInMs = 500,
                RunnerUri = new Uri($"https://localhost:{port}"),
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName, out _),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // execute the busy loop using the remote runner and confirm it throws TimeoutException
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                var result = await Dido.RunAsync<bool>((context) => BusyLoopWithCancellation.DoFakeWork(context), configuration);
            });

            // cleanup
            runnerServer.DeleteCache();
            runnerServer.Dispose();
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.RunAsync using a local loop-back runner server
        /// and an infinite-loop expression to confirm a canceled exception is thrown.
        /// </summary>
        [Fact]
        public async void RunRemoteWithCancel()
        {
            // create and start a secure localhost loop-back runner server that can execute serialized expressions
            var runnerServer = new RunnerServer();
            int port = GetNextAvailablePort();
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

            // create configuration to use the loop-back server
            var configuration = new Configuration
            {
                MaxTries = 1,
                RunnerUri = new Uri($"https://localhost:{port}"),
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName, out _),
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
            runnerServer.DeleteCache();
            runnerServer.Dispose();
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.RunAsync using a local loop-back runner server
        /// and a expression that throws during execution.
        /// </summary>
        [Fact]
        public async void RunRemoteWithException()
        {
            // create and start a secure localhost loop-back runner server that can execute serialized expressions
            var runnerServer = new RunnerServer();
            int port = GetNextAvailablePort();
            runnerServer.Start(TestSelfSignedCert.ServerCertificate, port, IPAddress.Loopback);

            // create configuration to use the loop-back server
            var configuration = new Configuration
            {
                MaxTries = 1,
                RunnerUri = new Uri($"https://localhost:{port}"),
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName, out _),
                // bypass server cert validation since unit tests are using a base-64 self-signed cert
                ServerCertificateValidationPolicy = ServerCertificateValidationPolicies._SKIP_
            };

            // execute the busy loop using the remote runner and confirm it throws the expected exception
            var message = Guid.NewGuid().ToString();
            var ex = await Assert.ThrowsAsync<TaskInvocationException>(async () =>
            {
                var result = await Dido.RunAsync<bool>(
                    (context) => SleepThenException.DoFakeWork(message),
                    configuration);
            });
            Assert.NotNull(ex.InnerException);
            Assert.Equal(typeof(InvalidOperationException), ex.InnerException!.GetType());
            Assert.Contains(message, ex.InnerException.Message);

            // cleanup
            runnerServer.DeleteCache();
            runnerServer.Dispose();
        }

        /// <summary>
        /// Performs an end-to-end test of Dido.RunAsync using local loop-back mediator and runner servers.
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

            // execute the lambda expression using the mediator to choose the runner
            var result = await Dido.RunAsync<int>(lambda, configuration);

            // confirm the results match
            Assert.Equal(expectedResult, result);

            // cleanup
            runnerServer.DeleteCache();
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