using DidoNet.Test.Common;
using System;
using System.IO;
using Xunit;

namespace DidoNet.Test.Runner
{
    public class DeserializationAndInvocationTests : IClassFixture<TestFixture>
    {
        readonly TestFixture TestFixture;

        // TODO: try creating a separate app domain 
        // https://docs.microsoft.com/en-us/dotnet/api/system.appdomain?view=net-6.0#:~:text=The%20AppDomain%20class%20implements%20a,application%20domains%2C%20see%20Application%20Domains.
        // TODO: appdomains deprecated, use AssemblyLoadContext instead?
        // https://stackoverflow.com/questions/27266907/no-appdomains-in-net-core-why

        public DeserializationAndInvocationTests(TestFixture fixture)
        {
            TestFixture = fixture;
        }

        [Fact]
        public async void TestMemberMethod()
        {
            // try to load the serialized member method lambda
            var path = Path.Combine(TestFixture.SharedTestDataPath, TestFixture.MemberMethodFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{path}'");
            }
            var bytes = File.ReadAllBytes(path);
            if (bytes == null)
            {
                throw new InvalidOperationException($"Could not load '{path}'");
            }

            // try to load the serialized member method result
            path = Path.Combine(TestFixture.SharedTestDataPath, TestFixture.MemberResultFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{path}'");
            }
            var expectedResult = Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(path));

            // deserialize the method lambda, using the custom resolver to resolve dependencies.
            // NOTE: the original saved expression was an int32, but here the return type is explicitly
            // being set to an int64 (long) because the json deserializer deserializes all integers to int64
            var method = await Dido.DeserializeAsync<long>(bytes, TestFixture.Environment);
            if (method == null)
            {
                throw new InvalidOperationException($"Could not deserialize method from '{path}'");
            }
            var actualResult = method.Invoke(TestFixture.Environment.ExecutionContext);

            Assert.Equal(expectedResult, actualResult);
        }

        [Fact]
        public async void TestStaticMethod()
        {
            // try to load the serialized static method lambda
            var path = Path.Combine(TestFixture.SharedTestDataPath, TestFixture.StaticMethodFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{path}'");
            }
            var bytes = File.ReadAllBytes(path);
            if (bytes == null)
            {
                throw new InvalidOperationException($"Could not load '{path}'");
            }

            // try to load the serialized static method result
            path = Path.Combine(TestFixture.SharedTestDataPath, TestFixture.StaticResultFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{path}'");
            }
            var expectedResult = Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(path));

            // deserialize the method lambda, using the custom resolver to resolve dependencies.
            // NOTE: the original saved expression was an int32, but here the return type is explicitly
            // being set to an int64 (long) because the json deserializer deserializes all integers to int64
            var method = await Dido.DeserializeAsync<long>(bytes, TestFixture.Environment);
            if (method == null)
            {
                throw new InvalidOperationException($"Could not deserialize method from '{path}'");
            }
            var actualResult = method.Invoke(TestFixture.Environment.ExecutionContext);

            Assert.Equal(expectedResult, actualResult);
        }

        [Fact]
        public async void TestMemberMethodWithDependency()
        {
            // try to load the serialized member method lambda
            var path = Path.Combine(TestFixture.SharedTestDataPath, TestFixture.DependencyMethodFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{path}'");
            }
            var bytes = File.ReadAllBytes(path);
            if (bytes == null)
            {
                throw new InvalidOperationException($"Could not load '{path}'");
            }

            // try to load the serialized member method result
            path = Path.Combine(TestFixture.SharedTestDataPath, TestFixture.DependencyResultFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{path}'");
            }
            var expectedResult = Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(path));

            // deserialize the method lambda, using the custom resolver to resolve dependencies
            var method = await Dido.DeserializeAsync<string>(bytes, TestFixture.Environment);
            if (method == null)
            {
                throw new InvalidOperationException($"Could not deserialize method from '{path}'");
            }
            var actualResult = method.Invoke(TestFixture.Environment.ExecutionContext);

            Assert.Equal(expectedResult, actualResult);
        }
    }
}