using AnywhereNET.Test.Common;
using AnywhereNET.TestLib;
using AnywhereNET.TestLibDependency;
using System;
using Xunit;

namespace AnywhereNET.Test
{
    public class SerializationAndInvocationTests : IClassFixture<AnywhereTestFixture>
    {
        readonly AnywhereTestFixture TestFixture;

        public SerializationAndInvocationTests(AnywhereTestFixture fixture)
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