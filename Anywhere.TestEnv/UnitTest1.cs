using Anywhere.Test.Common;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Anywhere.TestEnv
{
    public class UnitTest1 : IClassFixture<AnywhereTestFixture>
    {
        readonly AnywhereTestFixture TestFixture;

        /// <summary>
        /// The folder containing the assemblies of the sibling TestLib project.
        /// </summary>
        readonly string TestLibAssembliesFolder = FindTestLibAssembliesFolder();

        public UnitTest1(AnywhereTestFixture fixture)
        {
            TestFixture = fixture;
        }

        /// <summary>
        /// Find the folder containing the assemblies of the sibling TestLib project,
        /// which are needed by AssemblyResolver to dynamically resolve assemblies at runtime.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal static string FindTestLibAssembliesFolder()
        {
            // get current executing path and its root
            var path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var root = Path.GetPathRoot(path);

            // assuming the testlib is a sibling, find the path to its assemblies
            string? testLibFolder = null;
            while (!string.IsNullOrEmpty(path) && path != root && string.IsNullOrEmpty(testLibFolder))
            {
                path = Directory.GetParent(path!)!.FullName;

                // see if the testlib folder is a child of the path
                testLibFolder = Directory.EnumerateDirectories(path!).FirstOrDefault(d => d.EndsWith(AnywhereTestFixture.TestLibName));

                // if so, search it for the assemblies
                if (testLibFolder != null)
                {
                    var dll = Directory.GetFiles(testLibFolder, $"{AnywhereTestFixture.TestLibName}.{AnywhereTestFixture.AssemblyExtension}", SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (dll != null)
                    {
                        // found it
                        testLibFolder = Directory.GetParent(dll)?.FullName;
                    }
                }
            }

            if (testLibFolder == null)
            {
                throw new InvalidOperationException($"Could not find assemblies for project '{AnywhereTestFixture.TestLibName}'. Be sure that project is built and available.");
            }

            return testLibFolder;
        }

        /// <summary>
        /// Resolve and return the provided assembly.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        Task<Stream?> AssemblyResolver(string assemblyName)
        {
            // TODO: memoize this
            var files = Directory.EnumerateFiles(TestLibAssembliesFolder);
            foreach (var file in files.Where(f => f.ToLower().EndsWith(AnywhereTestFixture.AssemblyExtension)))
            {
                try
                {
                    AssemblyName name = AssemblyName.GetAssemblyName(file);
                    if (name.FullName == assemblyName)
                    {
                        return Task.FromResult<Stream?>(File.Open(file, FileMode.Open, FileAccess.Read));
                    }
                }
                catch (Exception)
                {
                    // exception will be thrown if the file is not a .NET assembly, in which case simply ignore
                    continue;
                }
            }

            // return null if no matching assembly could be found
            return Task.FromResult<Stream?>(null);
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
            var data = File.ReadAllText(path);
            if (data == null)
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
            var method = await MethodModelBuilder.DeserializeAsync(data, AssemblyResolver);
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
            var method = await MethodModelBuilder.DeserializeAsync(data, AssemblyResolver);
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
            var method = await MethodModelBuilder.DeserializeAsync(data, AssemblyResolver);
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