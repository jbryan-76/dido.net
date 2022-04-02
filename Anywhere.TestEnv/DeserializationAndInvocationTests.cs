using AnywhereNET.Test.Common;
using System;
using System.IO;
using Xunit;

namespace AnywhereNET.TestEnv
{
    public class DeserializationAndInvocationTests : IClassFixture<AnywhereTestFixture>
    {
        readonly AnywhereTestFixture TestFixture;

        // TODO: try creating a separate app domain 
        // https://docs.microsoft.com/en-us/dotnet/api/system.appdomain?view=net-6.0#:~:text=The%20AppDomain%20class%20implements%20a,application%20domains%2C%20see%20Application%20Domains.
        // TODO: appdomains deprecated, use AssemblyLoadContext instead?
        // https://stackoverflow.com/questions/27266907/no-appdomains-in-net-core-why

        public DeserializationAndInvocationTests(AnywhereTestFixture fixture)
        {
            TestFixture = fixture;
        }

        [Fact]
        public async void TestMemberMethod()
        {
            // try to load the serialized member method lambda
            var path = Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.MemberMethodFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{path}'");
            }
            var bytes = File.ReadAllBytes(path);
            //var data = File.ReadAllText(path);
            if (bytes == null)
            //if (data == null)
            {
                throw new InvalidOperationException($"Could not load '{path}'");
            }

            // try to load the serialized member method result
            path = Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.MemberResultFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{path}'");
            }
            var expectedResult = Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(path));

            // deserialize the method lambda, using the custom resolver to resolve dependencies
            // NOTE: the original saved expression was an int32, but here the return type is explicitly
            // being set to an int64 (long) because the json deserializer deserializes all integers to int64
            var method = await TestFixture.Anywhere.DeserializeNew<long>(bytes, TestFixture.Environment);
            //var method = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, data);//, AssemblyResolver);
            if (method == null)
            {
                throw new InvalidOperationException($"Could not deserialize method from '{path}'");
            }
            var actualResult = method.Invoke(TestFixture.Environment.Context);

            // invoke the method and confirm its result matches the expected result
            //var actualResult = method.Invoke();
            // serialize and deserialize the actual result to coerce to the same data type as the expected result
            // (otherwise eg comparing an int32 and int64 will fail the below assertion)
            //actualResult = Newtonsoft.Json.JsonConvert.DeserializeObject(Newtonsoft.Json.JsonConvert.SerializeObject(actualResult));

            Assert.Equal(expectedResult, actualResult);
        }

        [Fact]
        public async void TestStaticMethod()
        {
            // try to load the serialized static method lambda
            var path = Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.StaticMethodFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{path}'");
            }
            var data = File.ReadAllText(path);
            if (data == null)
            {
                throw new InvalidOperationException($"Could not load '{path}'");
            }

            // try to load the serialized static method result
            path = Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.StaticResultFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{path}'");
            }
            var expectedResult = Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(path));

            // deserialize the method lambda, using the custom resolver to resolve dependencies
            var method = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, data);//, AssemblyResolver);
            if (method == null)
            {
                throw new InvalidOperationException($"Could not deserialize method from '{path}'");
            }

            // invoke the method and confirm its result matches the expected result
            var actualResult = method.Invoke();
            // serialize and deserialize the actual result to coerce to the same data type as the expected result
            // (otherwise eg comparing an int32 and int64 will fail the below assertion)
            actualResult = Newtonsoft.Json.JsonConvert.DeserializeObject(Newtonsoft.Json.JsonConvert.SerializeObject(actualResult));

            Assert.Equal(expectedResult, actualResult);
        }

        [Fact]
        public async void TestMemberMethodWithDependency()
        {
            // try to load the serialized member method lambda
            var path = Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.DependencyMethodFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{path}'");
            }
            var data = File.ReadAllText(path);
            if (data == null)
            {
                throw new InvalidOperationException($"Could not load '{path}'");
            }

            // try to load the serialized member method result
            path = Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.DependencyResultFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{path}'");
            }
            var expectedResult = Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(path));

            // deserialize the method lambda, using the custom resolver to resolve dependencies
            var method = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, data);//, AssemblyResolver);
            if (method == null)
            {
                throw new InvalidOperationException($"Could not deserialize method from '{path}'");
            }

            // invoke the method and confirm its result matches the expected result
            var actualResult = method.Invoke();
            // serialize and deserialize the actual result to coerce to the same data type as the expected result
            // (otherwise eg comparing an int32 and int64 will fail the below assertion)
            actualResult = Newtonsoft.Json.JsonConvert.DeserializeObject(Newtonsoft.Json.JsonConvert.SerializeObject(actualResult));

            Assert.Equal(expectedResult, actualResult);
        }
    }
}