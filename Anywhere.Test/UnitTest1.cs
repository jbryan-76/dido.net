using System;
using System.Linq;
using System.Reflection;
using Xunit;
using AnywhereNET.TestLib;
using AnywhereNET.Test.Common;
using AnywhereNET.TestLibDependency;

namespace AnywhereNET.Test
{
    // Write app code normally, subject to the following constraints when code is executed with Anywhere:
    // - arguments to called method must be bi-directionally serializable
    // - return type of method must be bi-directionally serializable
    // - if method is a member method, calling instance must be bi-directionally serializable
    // - otherwise method is a static method and is not called on an instance

    // Anywhere (lib): models, public API, enums, callbacks, local execution, delegates
    // Anywhere.Env: an execution environment to run code
    // Anywhere.Orch: an orchestrator to distribute work among environments
    // Anywhere.Test: unit tests

    // comm between lib and env: network api? message queue?
    // delegate this to a wrapper layer

    // app usage: var anywhere = new Anywhere(CONFIG); await anywhere.Execute( LAMBDA )

    // CONFIG = local vs remote, remote endpoint, queues

    // immediate execution:
    // - app executes a lambda and awaits the result

    // eventual execution:
    // - option 1: app submits a lambda and awaits the result which is an id to poll for the status/result
    // - option 2: app submits a lambda and a callback which is invoked when the result is ready

    // execution sequence: 
    // - use reflection to serialize the lambda expression into a data blob containing all info to execute the lambda
    // - open a connection to the orchestrator
    // - transmit the blob
    // - deserialize the blob to an invokable method
    // - try to instantiate and execute the method
    // - catch exceptions for missing assemblies and request from source app as needed via connection
    // - return the result to the app via connection
    // in debug mode, do above regardless of local vs remote
    // in release mode with local execution, invoke lambda directly and bypass all serialization

    // GOTCHAS
    // - loading local files: maybe use anywhere overloads for IO namespace?
    // - using interop or OS-specific calls: allow but have to specify proper env for runtime?
    // - explicitly loading other assemblies: overloads?

    // app delegates configuration:
    // - persistence: where/how to store in-progress "jobs"
    // - communications: how the lib talks to the orch and env

    // testing:
    // - project 1: fake lib with models and methods to do work.
    // - project 2: fake app. import 

    public class UnitTest1 : IClassFixture<AnywhereTestFixture>
    {
        readonly AnywhereTestFixture TestFixture;

        public UnitTest1(AnywhereTestFixture fixture)
        {
            TestFixture = fixture;
        }

        // TODO: these fields can't be static. figure out why

        SampleDependencyClass DependencyModel = new SampleDependencyClass
        {
            MyString = "my string",
            MyModel = new SampleDependencyModel
            {
                MyBool = true,
                MyInt = 456,
                MyDateTimeOffset = DateTimeOffset.Now,
            }
        };

        int FakeArgument = 123;

        SampleWorkerClass FakeObject = new SampleWorkerClass();

        internal static string Foo(ExecutionContext context, int constantVal, string closureVal)
        {
            return constantVal.ToString() + closureVal;
        }

        /// <summary>
        /// Generate sample lambda method calls, then serialize and save them to a common shared folder
        /// to be deserialized and executed by the Anywhere.TestEnv project.
        /// </summary>
        [Fact]
        public void GenerateSerializedMethodInvocationData()
        {

            // serialize a lambda expression invoking a member method
            var data = TestFixture.Anywhere.Serialize((context) => FakeObject.SimpleMemberMethod(FakeArgument));
            // save the serialized model
            var path = System.IO.Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.MemberMethodFile);
            System.IO.File.WriteAllText(path, data);
            // save the expected result
            var result = FakeObject.SimpleMemberMethod(FakeArgument);
            path = System.IO.Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.MemberResultFile);
            System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(result));

            // serialize a lambda expression invoking a static method
            data = TestFixture.Anywhere.Serialize((context) => SampleWorkerClass.SimpleStaticMethod(FakeArgument));
            // save the serialized model
            path = System.IO.Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.StaticMethodFile);
            System.IO.File.WriteAllText(path, data);
            // save the expected result
            result = SampleWorkerClass.SimpleStaticMethod(FakeArgument);
            path = System.IO.Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.StaticResultFile);
            System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(result));

            // serialize a lambda expression that uses dependent assemblies
            data = TestFixture.Anywhere.Serialize((context) => FakeObject.MemberMethodWithDependency(DependencyModel));
            // save the serialized model
            path = System.IO.Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.DependencyMethodFile);
            System.IO.File.WriteAllText(path, data);
            // save the expected result
            var dependencyResult = FakeObject.MemberMethodWithDependency(DependencyModel);
            path = System.IO.Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.DependencyResultFile);
            System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(dependencyResult));
        }

        [Fact]
        public async void TestMemberMethod()
        {
            var data = TestFixture.Anywhere.Serialize((context) => FakeObject.SimpleMemberMethod(FakeArgument));
            //var method = MethodModelBuilder.Deserialize(data);
            var method = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, data);
            var result = method.Invoke();

            Assert.Equal(FakeArgument, result);
        }

        [Fact]
        public async void TestStaticMethod()
        {
            var data = TestFixture.Anywhere.Serialize((context) => SampleWorkerClass.SimpleStaticMethod(FakeArgument));
            var method = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, data);
            var result = method.Invoke();

            Assert.Equal(FakeArgument, result);
        }

        [Fact]
        public async void TestMemberMethodWithDependency()
        {
            var data = TestFixture.Anywhere.Serialize((context) => FakeObject.MemberMethodWithDependency(DependencyModel));
            var method = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, data);
            var actualResult = method.Invoke();
            var expectedResult = FakeObject.MemberMethodWithDependency(DependencyModel);

            Assert.Equal(expectedResult, actualResult);
        }

        [Fact]
        public async void TestStaticMethodWithContext()
        {
            string closureVal = "hello world";
            var data = TestFixture.Anywhere.Serialize((context) => Foo(context, 23, closureVal));
            var method = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, data);
            var actualResult = method.Invoke();
            var expectedResult = Foo(TestFixture.Environment.Context, 23, closureVal);

            Assert.Equal(expectedResult, actualResult);
        }
    }
}