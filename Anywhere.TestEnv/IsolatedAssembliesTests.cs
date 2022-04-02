using AnywhereNET.Test.Common;
using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace AnywhereNET.TestEnv
{
    public class MyModel
    {
        public static int MyStaticInt = 10;

        public int MyInt { get; set; }

        public bool MyBool { get; set; }

        public int DoubleMyInt()
        {
            return MyInt * 2;
        }

        public string MyMethod(int val)
        {
            return $"{val} {MyInt} {MyBool} {MyStaticInt} {DoubleMyInt()}";
        }
    }

    public class IsolatedAssembliesTests : IClassFixture<AnywhereTestFixture>
    {
        readonly AnywhereTestFixture TestFixture;

        public IsolatedAssembliesTests(AnywhereTestFixture fixture)
        {
            TestFixture = fixture;
        }


        //class Dummy
        //{
        //    public object TestObject;
        //    public int TestArgument = 123;
        //}

        [Fact]
        public async void CreateLambdaFromLoadedAssembly()
        {
            var context = new AssemblyLoadContext("TestContext", true);
            var testLibStream = await TestFixture.Anywhere.ResolveLocalAssemblyAsync("Anywhere.TestLib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var testLibAssembly = context.LoadFromStream(testLibStream);
            testLibStream.Dispose();
            var testLibDependencyStream = await TestFixture.Anywhere.ResolveLocalAssemblyAsync("Anywhere.TestLibDependency, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var testLibDependencyAssembly = context.LoadFromStream(testLibDependencyStream);
            testLibDependencyStream.Dispose();

            //var dummy = new Dummy();

            var sampleWorkerType = testLibAssembly.GetType("AnywhereNET.TestLib.SampleWorkerClass");
            var TestObject = testLibAssembly.CreateInstance("AnywhereNET.TestLib.SampleWorkerClass");
            var methodInfo = sampleWorkerType.GetMethod("SimpleMemberMethod");

            int TestArgument = 123;

            // an expression referring to 'this' object (ie the test class instance)
            //var thisObjEx = Expression.Constant(dummy);
            var objEx = Expression.Constant(TestObject);

            //// an expression referring to the "FakeObject" member of this class
            //var objEx = Expression.MakeMemberAccess(thisObjEx, typeof(Dummy).GetField(nameof(Dummy.TestObject), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

            //// an expression referring to the "FakeArgument" member of this class
            //var argEx = Expression.MakeMemberAccess(thisObjEx, typeof(Dummy).GetField(nameof(Dummy.TestArgument), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

            //var objEx = Expression.Constant(sampleWorkerObject);
            //int argument = 123;
            var argEx = Expression.Constant(TestArgument);
            var convertEx = Expression.Convert(objEx, sampleWorkerType);
            var bodyEx = Expression.Call(convertEx, methodInfo, argEx);

            var contextParamEx = Expression.Parameter(typeof(ExecutionContext), "context");

            // finally, the lambda expression that uses the single lambda parameter and executes the lambda body
            var lambda = Expression.Lambda<Func<ExecutionContext, int>>(bodyEx, contextParamEx);
            
            var expectedResult = lambda.Compile().Invoke(TestFixture.Environment.Context);

            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                // serialize the expression to simulate transmission on a stream
                await ExpressionSerializer.SerializeAsync(lambda, stream);
                bytes = stream.ToArray();
            }

            context.Unload();

            using (var stream = new MemoryStream(bytes))
            {
                // deserialize the stream to an invokable lambda
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                var decodedLambda = await ExpressionSerializer.DeserializeAsync<object>(stream, TestFixture.Environment);
                
                // invoke the lambda and confirm the result
                var result = decodedLambda.Invoke(TestFixture.Environment.Context);
                Assert.Equal(expectedResult, result);
            }
        }

        [Fact]
        public async void EndToEnd()
        {
            string Base64SelfSignedCert = "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";
            X509Certificate2 ServerCertificate = new X509Certificate2(Convert.FromBase64String(Base64SelfSignedCert), "1234");

            // TODO: create a loopback server connection and channel(s)
            // TODO: create a loopback client connection and channel(s)
            // TODO: 
            // TODO: load the serialized lambdas
            // TODO: create a lambda from scratch?
            // TODO: remoteExecute the lambda
            // TODO: "send" to the env
            // TODO: "receive" it, deserialize, invoke, etc
            // TODO: catch assembly errors. request assembly on one channel, send back on another




        }

        //[Fact]
        //public async void Foo()
        //{
        //    var stream = await TestFixture.Anywhere.ResolveLocalAssemblyAsync("Anywhere.TestLibDependency, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

        //    var context1 = new AssemblyLoadContext("Test 1", true);
        //    var asm1 = context1.LoadFromStream(stream);
        //    var obj1 = asm1.CreateInstance("AnywhereNET.TestLibDependency.SampleDependencyModel");
        //    obj1.GetType().GetMethod("Increment", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        //    var info1 = obj1.GetType().GetField("MyStaticInt");
        //    var myStaticInt1 = info1.GetValue(obj1);

        //    var context2 = new AssemblyLoadContext("Test 2", true);
        //    stream.Position = 0;
        //    var asm2 = context2.LoadFromStream(stream);
        //    var obj2 = asm2.CreateInstance("AnywhereNET.TestLibDependency.SampleDependencyModel");
        //    var info2 = obj2.GetType().GetField("MyStaticInt");
        //    var myStaticInt2 = info2.GetValue(obj1);
        //}

        // TODO: load a server in one context and a client in another context and make them 
        // TODO: do a full end-to-end serialize/invoke cycle

        //[Fact]
        //public async void Bar()
        //{
        //    //var anywhere = new Anywhere();
        //    var asmStream1 = await TestFixture.Anywhere.ResolveLocalAssemblyAsync("Anywhere.TestLibDependency, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
        //    var context1 = new AssemblyLoadContext("Test 1", true);
        //    var asm1 = context1.LoadFromStream(asmStream1);

        //    var all = AssemblyLoadContext.All;

        //    //var obj1 = asm1.CreateInstance("AnywhereNET.TestLibDependency.SampleDependencyModel");
        //    var type1 = asm1.GetType("AnywhereNET.TestLibDependency.SampleDependencyModel");
        //    //var type1 = Type.GetType("AnywhereNET.TestLibDependency.SampleDependencyModel"); // this won't work unless the assembly is loaded into the Default context
        //    var obj1 = Activator.CreateInstance(type1);
        //    obj1.GetType().GetMethod("Increment", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        //    var info1 = obj1.GetType().GetField("MyStaticInt");
        //    var myStaticInt1 = info1.GetValue(obj1);

        //    var asmStream2 = await TestFixture.Anywhere.ResolveLocalAssemblyAsync("Anywhere.TestLib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
        //    var context2 = new AssemblyLoadContext("Test 2", true);
        //    var asm2 = context2.LoadFromStream(asmStream2);
        //    var type2 = asm2.GetType("AnywhereNET.TestLib.SampleModelClass");
        //    //var type2 = Type.GetType("AnywhereNET.TestLib.SampleModelClass"); // this won't work unless the assembly is loaded into the Default context
        //    var obj2 = Activator.CreateInstance(type2);
        //    //var obj2 = asm2.CreateInstance("AnywhereNET.TestLib.SampleModelClass");
        //    var info2 = obj2.GetType().GetMethod("MySetIntMethod");
        //    var result = info2.Invoke(obj2, new object[] { 23 });
        //    //var myStaticInt2 = info2.GetValue(obj1);
        //}
    }
}