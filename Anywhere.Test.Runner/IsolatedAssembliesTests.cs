using DidoNet.Test.Common;
using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using Xunit;

namespace DidoNet.Test.Runner
{
    public class IsolatedAssembliesTests : IClassFixture<AnywhereTestFixture>
    {
        static long NextPort = 9000;

        /// <summary>
        /// Gets a unique port number so multiple client/server tests can run simultaneously.
        /// </summary>
        /// <returns></returns>
        internal static int GetNextAvailablePort()
        {
            return (int)Interlocked.Increment(ref NextPort);
        }

        readonly AnywhereTestFixture TestFixture;

        public IsolatedAssembliesTests(AnywhereTestFixture fixture)
        {
            TestFixture = fixture;
        }

        [Fact]
        public async void CreateLambdaFromDynamicLoadedAssembly_LocalClosure()
        {
            // create a unique isolated context to load assemblies
            var context = new AssemblyLoadContext(Guid.NewGuid().ToString(), true);

            // dynamically load the 2 test assemblies into the context
            var testLibStream = await TestFixture.Configuration.ResolveLocalAssemblyAsync("Anywhere.TestLib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var testLibAssembly = context.LoadFromStream(testLibStream);
            testLibStream.Dispose();
            var testLibDependencyStream = await TestFixture.Configuration.ResolveLocalAssemblyAsync("Anywhere.TestLibDependency, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var testLibDependencyAssembly = context.LoadFromStream(testLibDependencyStream);
            testLibDependencyStream.Dispose();

            // get the test class type and method to be used below in the dynamically created expression
            var sampleWorkerType = testLibAssembly.GetType("DidoNet.TestLib.SampleWorkerClass");
            var methodInfo = sampleWorkerType.GetMethod("SimpleMemberMethod");

            // create the test instance and sample method argument
            var testObject = testLibAssembly.CreateInstance("DidoNet.TestLib.SampleWorkerClass");
            int testArgument = 123;

            // a constant referring to the local SampleWorkerClass test object
            var objEx = Expression.Constant(testObject);

            // a constant referring to the local int test argument
            var argEx = Expression.Constant(testArgument);

            // cast the generic test object to a concrete type (SampleWorkerClass)
            var convertEx = Expression.Convert(objEx, sampleWorkerType);

            // call a method (SimpleMemberMethod) on the cast object, passing the argument
            var bodyEx = Expression.Call(convertEx, methodInfo, argEx);

            // the one and only supported parameter to the lambda function: the ExecutionContext
            var contextParamEx = Expression.Parameter(typeof(ExecutionContext), "context");

            // finally, the lambda expression that uses the single parameter and executes the body
            var lambda = Expression.Lambda<Func<ExecutionContext, int>>(bodyEx, contextParamEx);

            // compile and execute the lambda to get the expected result
            var expectedResult = lambda.Compile().Invoke(TestFixture.Environment.ExecutionContext);

            // serialize the lambda expression to simulate transmission on a stream
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                await ExpressionSerializer.SerializeAsync(lambda, stream);
                bytes = stream.ToArray();
            }

            // unload the assembly context to be sure all needed assemblies are resolved dynamically
            context.Unload();

            using (var stream = new MemoryStream(bytes))
            {
                // deserialize the stream to an invokable lambda
                var decodedLambda = await ExpressionSerializer.DeserializeAsync<object>(stream, TestFixture.Environment);

                // invoke the lambda and confirm the result
                var result = decodedLambda.Invoke(TestFixture.Environment.ExecutionContext);
                Assert.Equal(expectedResult, result);
            }
        }

        class Dummy
        {
            public object TestObject;
            public int TestArgument = 123;
        }

        Dummy dummy = new Dummy();

        [Fact]
        public async void CreateLambdaFromDynamicLoadedAssembly_AmbientClosure()
        {
            // create a unique isolated context to load assemblies
            var context = new AssemblyLoadContext(Guid.NewGuid().ToString(), true);

            // dynamically load the 2 test assemblies into the context
            var testLibStream = await TestFixture.Configuration.ResolveLocalAssemblyAsync("Anywhere.TestLib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var testLibAssembly = context.LoadFromStream(testLibStream);
            testLibStream.Dispose();
            var testLibDependencyStream = await TestFixture.Configuration.ResolveLocalAssemblyAsync("Anywhere.TestLibDependency, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var testLibDependencyAssembly = context.LoadFromStream(testLibDependencyStream);
            testLibDependencyStream.Dispose();

            // get the test class type and method to be used below in the dynamically created expression
            var sampleWorkerType = testLibAssembly.GetType("DidoNet.TestLib.SampleWorkerClass");
            var methodInfo = sampleWorkerType.GetMethod("SimpleMemberMethod");

            // create the test instance and sample method argument
            dummy.TestObject = testLibAssembly.CreateInstance("DidoNet.TestLib.SampleWorkerClass");
            dummy.TestArgument = 456;

            // a constant referring to "this" object (ie the test class instance)
            var thisObjEx = Expression.Constant(this);

            // access the "dummy" field of this object
            var dummyObjEx = Expression.MakeMemberAccess(thisObjEx, typeof(IsolatedAssembliesTests).GetField(nameof(dummy), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)); ;

            // access the "TestObject" member
            var objEx = Expression.MakeMemberAccess(dummyObjEx, typeof(Dummy).GetField(nameof(Dummy.TestObject), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

            // access the "TestArgument" member
            var argEx = Expression.MakeMemberAccess(dummyObjEx, typeof(Dummy).GetField(nameof(Dummy.TestArgument), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

            // cast the generic test object to a concrete type (SampleWorkerClass)
            var convertEx = Expression.Convert(objEx, sampleWorkerType);

            // call a method (SimpleMemberMethod) on the cast object, passing the argument
            var bodyEx = Expression.Call(convertEx, methodInfo, argEx);

            // the one and only supported parameter to the lambda function: the ExecutionContext
            var contextParamEx = Expression.Parameter(typeof(ExecutionContext), "context");

            // finally, the lambda expression that uses the single parameter and executes the body
            var lambda = Expression.Lambda<Func<ExecutionContext, int>>(bodyEx, contextParamEx);

            // compile and execute the lambda to get the expected result
            var expectedResult = lambda.Compile().Invoke(TestFixture.Environment.ExecutionContext);

            // serialize the lambda expression to simulate transmission on a stream
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                await ExpressionSerializer.SerializeAsync(lambda, stream);
                bytes = stream.ToArray();
            }

            // unload the assembly context to be sure all needed assemblies are resolved dynamically
            context.Unload();

            using (var stream = new MemoryStream(bytes))
            {
                // deserialize the stream to an invokable lambda
                var decodedLambda = await ExpressionSerializer.DeserializeAsync<object>(stream, TestFixture.Environment);

                // invoke the lambda and confirm the result
                var result = decodedLambda.Invoke(TestFixture.Environment.ExecutionContext);
                Assert.Equal(expectedResult, result);
            }
        }
    }
}