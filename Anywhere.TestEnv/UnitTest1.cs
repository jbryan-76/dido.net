using Anywhere.Test.Common;
using System;
using System.Linq;
using Xunit;

namespace Anywhere.TestEnv
{
    public class UnitTest1 : IClassFixture<AnywhereTestFixture>
    {
        readonly AnywhereTestFixture TestFixture;

        public UnitTest1(AnywhereTestFixture fixture)
        {
            TestFixture = fixture;
        }

        internal string? FindTestLibAssembliesFolder()
        {
            // get current executing path and its root
            var path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var root = System.IO.Path.GetPathRoot(path);

            // assuming the testlib is a sibling, find the path to its assemblies
            string? testLibFolder = null;
            while (!string.IsNullOrEmpty(path) && path != root && string.IsNullOrEmpty(testLibFolder))
            {
                path = System.IO.Directory.GetParent(path!)!.FullName;

                // see if the testlib folder is a child of the path
                testLibFolder = System.IO.Directory.EnumerateDirectories(path!).FirstOrDefault(d => d.EndsWith(AnywhereTestFixture.TestLibName));

                // if so, search it for the assemblies
                if (testLibFolder != null)
                {
                    var dll = System.IO.Directory.GetFiles(testLibFolder, $"{AnywhereTestFixture.TestLibName}.{AnywhereTestFixture.AssemblyExtension}", System.IO.SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (dll != null)
                    {
                        // found it
                        testLibFolder = System.IO.Directory.GetParent(dll)?.FullName;
                    }
                }
            }

            return testLibFolder;
        }

        [Fact]
        public void Test1()
        {
            var testDataPath = System.IO.Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.MemberMethodFile);
            if (!System.IO.File.Exists(testDataPath))
            {
                throw new InvalidOperationException($"Could not find pre-requisite '{testDataPath}'");
            }

            var data = System.IO.File.ReadAllText(testDataPath);
            if( data == null)
            {
                throw new InvalidOperationException($"Could not load '{testDataPath}'");
            }

            var method = MethodModelBuilder.Deserialize(data);
            if( method == null )
            {
                throw new InvalidOperationException($"Could not deserialize method from '{testDataPath}'");
            }

            var result = method.Invoke();

            //var method = new MethodModel
            //{
            //    MethodName = "MemberMethod",
            //    IsStatic = false,
            //    Instance = new ArgumentModel
            //    {
            //        Type = new TypeModel
            //        {
            //            Name = "Anywhere.TestLib.FakeUtil",
            //            AssemblyName = "Anywhere.TestLib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
            //            RuntimeVersion = "v4.0.30319"
            //        }
            //    }
            //};

            //// find the folder containing the test lib assemblies
            //var testLibAssembliesFolder = FindTestLibAssembliesFolder();
            //if (string.IsNullOrEmpty(testLibAssembliesFolder))
            //{
            //    throw new InvalidOperationException($"Could not locate test lib assemblies for {TestLibName}");
            //}

            //// manually load the assembly to instantiate an instance
            //var testLibAssemblyFile = $"{testLibAssembliesFolder}/{TestLibName}.{AssemblyExtension}";
            //var asm = Assembly.LoadFrom(testLibAssemblyFile);
            //if (asm == null)
            //{
            //    throw new InvalidOperationException($"Could not load test lib assembly from {testLibAssemblyFile}");
            //}

        }
    }
}