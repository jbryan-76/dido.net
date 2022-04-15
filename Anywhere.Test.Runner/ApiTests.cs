using AnywhereNET.Test.Common;
using System;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace AnywhereNET.Test.Runner
{
    public class ApiTests : IClassFixture<AnywhereTestFixture>
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

        readonly AnywhereTestFixture TestFixture;

        public ApiTests(AnywhereTestFixture fixture, ITestOutputHelper output)
        {
            TestFixture = fixture;
            //var converter = new OutputConverter(output, "OUTPUT.txt");
            //Console.SetOut(converter);
        }

        /// <summary>
        /// Performs an end-to-end test of Anywhere.RemoteExecuteAsync using a local loopback server.
        /// </summary>
        [Fact]
        public async void RemoteExecute()
        {
            // create a test lambda expression
            int testArgument = 123;
            var lambda = await CreateTestLambdaAsync(testArgument);

            // compile and execute the lambda to get the expected result and confirm it matches expectations
            var expectedResult = lambda.Compile().Invoke(TestFixture.Environment.ExecutionContext!);
            Assert.Equal(testArgument, expectedResult);

            // create and start a secure localhost loopback runner server that can execute serialized expressions
            string Base64SelfSignedCert = "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";
            var cert = new X509Certificate2(Convert.FromBase64String(Base64SelfSignedCert), "1234");
            var runnerServer = new RunnerServer();
            int port = GetNextAvailablePort();
            await runnerServer.Start(cert, port, IPAddress.Loopback);

            // debounce the server by giving it a beat or two to startup
            Thread.Sleep(10);

            // create an Anywhere instance configured to use the loopback server
            var configuration = new AnywhereConfiguration
            {
                OrchestratorUri = new Uri($"https://localhost:{port}"),
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName)
            };
            var anywhere = new Anywhere(configuration);

            // execute the lambda expression using the remote runner server
            var result = await anywhere.ExecuteAsync<int>(lambda);

            // confirm the results match
            Assert.Equal(expectedResult, result);

            // cleanup
            runnerServer.Dispose();
        }

        /// <summary>
        /// Performs an end-to-end test of Anywhere.RemoteExecuteAsync using a local loopback server.
        /// </summary>
        [Fact]
        public async void RemoteExecuteWithDeferredResultHandling()
        {
            // create a test lambda expression
            int testArgument = 123;
            var lambda = await CreateTestLambdaAsync(testArgument);

            // compile and execute the lambda to get the expected result and confirm it matches expectations
            var expectedResult = lambda.Compile().Invoke(TestFixture.Environment.ExecutionContext!);
            Assert.Equal(testArgument, expectedResult);

            // create and start a secure localhost loopback runner server that can execute serialized expressions
            string Base64SelfSignedCert = "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";
            var cert = new X509Certificate2(Convert.FromBase64String(Base64SelfSignedCert), "1234");
            var runnerServer = new RunnerServer();
            int port = GetNextAvailablePort();
            await runnerServer.Start(cert, port, IPAddress.Loopback);

            // create an Anywhere instance configured to use the loopback server
            var configuration = new AnywhereConfiguration
            {
                OrchestratorUri = new Uri($"https://localhost:{port}"),
                ExecutionMode = ExecutionModes.Remote,
                // use the unit test assembly resolver instead of the default implementation
                ResolveLocalAssemblyAsync = (assemblyName) => TestFixture.AssemblyResolver.ResolveAssembly(TestFixture.Environment, assemblyName)
            };
            var anywhere = new Anywhere(configuration);

            // execute the lambda expression using the remote runner server using the deferred handler
            // (NOTE the handler is run in a different thread)
            var reset = new AutoResetEvent(false);
            anywhere.Execute(lambda, (result) =>
            {
                // confirm the results match
                Assert.Equal(expectedResult, result);

                // signal the main thread that it can proceed
                reset.Set();
            });

            // block here until the result is received
            reset.WaitOne();

            // cleanup
            runnerServer.Dispose();
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
            var testLibStream = await TestFixture.Configuration.ResolveLocalAssemblyAsync("Anywhere.TestLib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var testLibAssembly = context.LoadFromStream(testLibStream);
            testLibStream.Dispose();
            var testLibDependencyStream = await TestFixture.Configuration.ResolveLocalAssemblyAsync("Anywhere.TestLibDependency, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var testLibDependencyAssembly = context.LoadFromStream(testLibDependencyStream);
            testLibDependencyStream.Dispose();
            var sampleWorkerType = testLibAssembly.GetType("AnywhereNET.TestLib.SampleWorkerClass");
            var methodInfo = sampleWorkerType!.GetMethod("SimpleMemberMethod")!;
            var testObject = testLibAssembly.CreateInstance("AnywhereNET.TestLib.SampleWorkerClass");
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