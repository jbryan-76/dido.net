using System;
using System.Linq;
using System.Reflection;
using Xunit;
using Anywhere.TestLib;
using Anywhere.Test.Common;

namespace Anywhere.Test
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

        [Fact]
        public void GenerateSerializedMethodInvocationData()
        {
            // serialize a lambda expression invoking a member method
            var fakeObj = new FakeUtil();
            var fakeArg = 123;
            var data = MethodModelBuilder.Serialize(() => fakeObj.MemberMethod(fakeArg));
            var testDataPath = System.IO.Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.MemberMethodFile);
            System.IO.File.WriteAllText(testDataPath, data);

            data = MethodModelBuilder.Serialize(() => FakeUtil.StaticMethod(fakeArg));
            testDataPath = System.IO.Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.StaticMethodFile);
            System.IO.File.WriteAllText(testDataPath, data);
        }

        [Fact]
        public void TestMemberMethod()
        {
            var fakeObj = new FakeUtil();
            var fakeArg = 123;

            var data = MethodModelBuilder.Serialize(() => fakeObj.MemberMethod(fakeArg));
            var method = MethodModelBuilder.Deserialize(data);
            var result = method.Invoke();

            Assert.Equal(fakeArg, result);

            //var type = typeof(FakeUtil);
            //var obj = Activator.CreateInstance(type);
            //var meth = type.GetMethod(nameof(FakeUtil.MemberMethod));


            // unload the assembly
        }

        [Fact]
        public void TestStaticMethod()
        {
            var fakeArg = 123;

            var data = MethodModelBuilder.Serialize(() => FakeUtil.StaticMethod(fakeArg));
            var method = MethodModelBuilder.Deserialize(data);
            var result = method.Invoke();

            Assert.Equal(fakeArg, result);

            //var type = typeof(FakeUtil);
            //var obj = Activator.CreateInstance(type);
            //var meth = type.GetMethod(nameof(FakeUtil.MemberMethod));


            // unload the assembly
        }

    }
}