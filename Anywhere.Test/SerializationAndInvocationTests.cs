using AnywhereNET.Test.Common;
using AnywhereNET.TestLib;
using AnywhereNET.TestLibDependency;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
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
            return constantVal.ToString() + closureVal + context.ExecutionMode.ToString();
        }

        /// <summary>
        /// Generate a variety of sample lambda method calls, then serialize and save them to a common shared folder
        /// to be deserialized and executed by the Anywhere.TestEnv project.
        /// <para/>
        /// NOTE this unit test must be run before any test from Anywhere.TestEnv.DeserializationAndInvocationTests.
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

        /// <summary>
        /// This test verifies a lambda invoking a member method on a class is properly 
        /// (de)serialized and invoked.
        /// </summary>
        [Fact]
        public async void TestMemberMethod()
        {
            FakeArgument = 456;
            var data = await TestFixture.Anywhere.SerializeNew((context) => FakeObject.SimpleMemberMethod(FakeArgument));
            var lambda = await TestFixture.Anywhere.DeserializeNew<int>(data, TestFixture.Environment);
            var result = lambda.Invoke(TestFixture.Environment.Context);

            //var data = TestFixture.Anywhere.Serialize((context) => FakeObject.SimpleMemberMethod(FakeArgument));
            //var data = TestFixture.Anywhere.Serialize((context) => FakeObject.SimpleMemberMethod(FakeArgument));
            ////var method = MethodModelBuilder.Deserialize(data);
            //var method = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, data);
            //var result = method.Invoke();

            Assert.Equal(FakeArgument, result);
        }

        [Fact]
        public async void TestLocalObjects()
        {
            var obj = new SampleWorkerClass();
            int arg = 123;

            var data = await TestFixture.Anywhere.SerializeNew((context) => obj.SimpleMemberMethod(arg));
            var lambda = await TestFixture.Anywhere.DeserializeNew<int>(data, TestFixture.Environment);
            var result = lambda.Invoke(TestFixture.Environment.Context);

            //var data = TestFixture.Anywhere.Serialize((context) => obj.SimpleMemberMethod(arg));
            //var method = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, data);
            //var result = method.Invoke();

            Assert.Equal(arg, result);
        }

        [Fact]
        public async void TestLambdaSerializeToJson()
        {
            // set up the test objects
            object obj = new SampleWorkerClass();
            int arg = 123;
            var expectedResult = ((SampleWorkerClass)obj).SimpleMemberMethod(arg);

            // encode a lambda expression to a serializable model
            var transmittedModel = ExpressionSerializer.Encode((context) => ((SampleWorkerClass)obj).SimpleMemberMethod(arg));

            // serialize and deserialize the model to simulate transmitting and receiving it
            ExpressionSerializer.Node receivedModel;
            using (var stream = new MemoryStream())
            {
                var settings = new ExpressionSerializer.SerializeSettings
                {
                    Format = ExpressionSerializer.SerializeSettings.Formats.Json
                };
                ExpressionSerializer.Serialize(transmittedModel, stream, settings);

                var bytes = stream.ToArray();
                var json = System.Text.Encoding.UTF8.GetString(bytes);

                stream.Position = 0;
                receivedModel = ExpressionSerializer.Deserialize(stream, settings);
            }

            // decode the model back to a lambda expression and execute it
            var lambda = await ExpressionSerializer.DecodeAsync<object>(receivedModel, TestFixture.Environment);
            var result = lambda.Invoke(TestFixture.Environment.Context);

            // confirm the result
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async void TestLambdaSerializeToBson()
        {
            // set up the test objects
            object obj = new SampleWorkerClass();
            int arg = 123;
            var expectedResult = ((SampleWorkerClass)obj).SimpleMemberMethod(arg);

            // serialize and deserialize the model to simulate transmitting and receiving it
            using (var stream = new MemoryStream())
            {
                var settings = new ExpressionSerializer.SerializeSettings
                {
                    Format = ExpressionSerializer.SerializeSettings.Formats.Bson
                };

                // serialize a lambda expression
                await ExpressionSerializer.SerializeAsync((context) => ((SampleWorkerClass)obj).SimpleMemberMethod(arg), stream, settings);

                // deserialize it
                stream.Position = 0;
                var lambda = await ExpressionSerializer.DeserializeAsync<object>(stream, TestFixture.Environment, settings);

                // execute it
                var result = lambda.Invoke(TestFixture.Environment.Context);

                // confirm the result
                Assert.Equal(expectedResult, result);
            }
        }

        /// <summary>
        /// This test verifies a lambda invoking a static method on a class is properly 
        /// (de)serialized and invoked.
        /// </summary>
        [Fact]
        public async void TestStaticMethod()
        {
            FakeArgument = 456;
            var data = await TestFixture.Anywhere.SerializeNew((context) => SampleWorkerClass.SimpleStaticMethod(FakeArgument));
            var lambda = await TestFixture.Anywhere.DeserializeNew<int>(data, TestFixture.Environment);
            var result = lambda.Invoke(TestFixture.Environment.Context);

            //var data = TestFixture.Anywhere.Serialize((context) => SampleWorkerClass.SimpleStaticMethod(FakeArgument));
            //var method = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, data);
            //var result = method.Invoke();

            Assert.Equal(FakeArgument, result);
        }

        /// <summary>
        /// This test verifies a lambda using code with additional assembly dependencies is properly 
        /// (de)serialized and invoked.
        /// </summary>
        [Fact]
        public async void TestMemberMethodWithClosureDependency()
        {
            var depModel = new SampleDependencyClass
            {
                MyString = "my string",
                MyModel = new SampleDependencyModel
                {
                    MyBool = true,
                    MyInt = 456,
                    MyDateTimeOffset = DateTimeOffset.Now,
                }
            };
            var data = await TestFixture.Anywhere.SerializeNew((context) => FakeObject.MemberMethodWithDependency(depModel));
            //var data = await TestFixture.Anywhere.SerializeNew((context) => FakeObject.MemberMethodWithDependency(DependencyModel));
            var lambda = await TestFixture.Anywhere.DeserializeNew<string>(data, TestFixture.Environment);
            var actualResult = lambda.Invoke(TestFixture.Environment.Context);

            //var data = TestFixture.Anywhere.Serialize((context) => FakeObject.MemberMethodWithDependency(DependencyModel));
            //var method = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, data);
            //var actualResult = method.Invoke();
            var expectedResult = FakeObject.MemberMethodWithDependency(depModel);
            //var expectedResult = FakeObject.MemberMethodWithDependency(DependencyModel);

            Assert.Equal(expectedResult, actualResult);
        }

        /// <summary>
        /// This test verifies a lambda using code with additional assembly dependencies is properly 
        /// (de)serialized and invoked.
        /// </summary>
        [Fact]
        public async void TestMemberMethodWithDependency()
        {
            var data = await TestFixture.Anywhere.SerializeNew((context) => FakeObject.MemberMethodWithDependency(DependencyModel));
            var lambda = await TestFixture.Anywhere.DeserializeNew<string>(data, TestFixture.Environment);
            var actualResult = lambda.Invoke(TestFixture.Environment.Context);
            var expectedResult = FakeObject.MemberMethodWithDependency(DependencyModel);
            Assert.Equal(expectedResult, actualResult);
        }

        /// <summary>
        /// This test verifies a lambda using the ambient ExecutionContext is properly 
        /// (de)serialized and invoked.
        /// </summary>
        [Fact]
        public async void TestStaticMethodWithContext()
        {
            string closureVal = "hello world";

            var data = await TestFixture.Anywhere.SerializeNew((context) => Foo(context, 23, closureVal));
            var lambda = await TestFixture.Anywhere.DeserializeNew<string>(data, TestFixture.Environment);
            var actualResult = lambda.Invoke(TestFixture.Environment.Context);

            //var data = TestFixture.Anywhere.Serialize((context) => Foo(context, 23, closureVal));
            //var method = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, data);
            //var actualResult = method.Invoke();
            var expectedResult = Foo(TestFixture.Environment.Context, 23, closureVal);

            Assert.Equal(expectedResult, actualResult);
        }

        /// <summary>
        /// This test verifies an explicitly created lambda expression matches an equivalent
        /// lambda expression in code. This is critical to confirm some tests in Anywhere.TestEnv
        /// that will not have the necessary assemblies loaded.
        /// </summary>
        [Fact]
        public async void TestExplicityCreatedLambda()
        {
            // this is the target expected lambda expression, which is created using the assemblies and models
            // already loaded as dependencies to this unit test project. like the other unit tests
            // in this class, it is simply a lambda that invokes a single-argument method on an object.
            Expression<Func<ExecutionContext, int>> expectedLambda =
                (context) => FakeObject.SimpleMemberMethod(FakeArgument);

            // the below statements are explicitly creating a lambda expression that is
            // equivalent to the target expression above, except technically without needing the actual
            // assemblies containing the referenced classes to be loaded

            // an expression referring to 'this' object (ie the test class instance)
            var thisObjEx = Expression.Constant(this);

            // an expression referring to the "FakeObject" member of this class
            var objEx = Expression.MakeMemberAccess(thisObjEx, typeof(SerializationAndInvocationTests).GetField(nameof(FakeObject), BindingFlags.Instance | BindingFlags.NonPublic));

            // an expression referring to the "FakeArgument" member of this class
            var argEx = Expression.MakeMemberAccess(thisObjEx, typeof(SerializationAndInvocationTests).GetField(nameof(FakeArgument), BindingFlags.Instance | BindingFlags.NonPublic));

            // the method info for the member method "SimpleMemberMethod" of the SampleWorkerClass
            var methodInfo = typeof(SampleWorkerClass).GetMethod(nameof(SampleWorkerClass.SimpleMemberMethod));

            // an expression to call the member method on the FakeObject instance using the FakeArgument argument
            var bodyEx = Expression.Call(objEx, methodInfo, argEx);

            // an expression referring to the ExecutionContext lambda parameter
            var contextParamEx = Expression.Parameter(typeof(ExecutionContext), "context");

            // finally, the lambda expression that uses the single lambda parameter and executes the lambda body
            var lambda = Expression.Lambda<Func<ExecutionContext, int>>(bodyEx, contextParamEx);

            // serialize both lambdas and confirm they match
            var expectedData2 = await TestFixture.Anywhere.SerializeNew(expectedLambda);
            var actualData2 = await TestFixture.Anywhere.SerializeNew(lambda);
            Assert.True(System.Linq.Enumerable.SequenceEqual(expectedData2, actualData2));

            //var expectedData = TestFixture.Anywhere.Serialize(expectedLambda);
            //var actualData = TestFixture.Anywhere.Serialize(lambda);
            //Assert.Equal(expectedData, actualData);

            // deserialize and execute both lambdas and confirm the results match
            var expectedMethod2 = await TestFixture.Anywhere.DeserializeNew<int>(expectedData2, TestFixture.Environment);
            var expectedResult2 = expectedMethod2.Invoke(TestFixture.Environment.Context);
            var actualMethod2 = await TestFixture.Anywhere.DeserializeNew<int>(actualData2, TestFixture.Environment);
            var actualResult2 = actualMethod2.Invoke(TestFixture.Environment.Context);
            Assert.Equal(expectedResult2, actualResult2);

            //var expectedMethod = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, expectedData);
            //var expectedResult = expectedMethod.Invoke();
            //var actualMethod = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, actualData);
            //var actualResult = actualMethod.Invoke();

            //Assert.Equal(expectedResult, actualResult);
        }

        //[Fact]
        //public async void TestExplicityCreatedLambda2()
        //{
        //    var obj = new SampleWorkerClass();
        //    int arg = 123;

        //    // this is the target expected lambda expression, which is created using the assemblies and models
        //    // already loaded as dependencies to this unit test project. like the other unit tests
        //    // in this class, it is simply a lambda that invokes a single-argument method on an object.
        //    Expression<Func<ExecutionContext, int>> expectedLambda =
        //        (context) => obj.SimpleMemberMethod(arg);

        //    // the below statements are explicitly creating a lambda expression that is
        //    // equivalent to the target expression above, except technically without needing the actual
        //    // assemblies containing the referenced classes to be loaded

        //    //// an expression referring to 'this' object (ie the test class instance)
        //    //var thisObjEx = Expression.Constant(this);

        //    //// an expression referring to the "FakeObject" member of this class
        //    //var objEx = Expression.MakeMemberAccess(thisObjEx, typeof(SerializationAndInvocationTests).GetField(nameof(FakeObject), BindingFlags.Instance | BindingFlags.NonPublic));

        //    //// an expression referring to the "FakeArgument" member of this class
        //    //var argEx = Expression.MakeMemberAccess(thisObjEx, typeof(SerializationAndInvocationTests).GetField(nameof(FakeArgument), BindingFlags.Instance | BindingFlags.NonPublic));

        //    //var objEx = Expression.MakeMemberAccess(thisObjEx, typeof(SerializationAndInvocationTests).GetField(nameof(FakeObject), BindingFlags.Instance | BindingFlags.NonPublic));
        //    var objEx = Expression.Variable(typeof(SampleWorkerClass));
        //    var argEx = Expression.Variable(typeof(int));
        //    //var argEx = Expression.Constant(arg);

        //    // the method info for the member method "SimpleMemberMethod" of the SampleWorkerClass
        //    var methodInfo = typeof(SampleWorkerClass).GetMethod(nameof(SampleWorkerClass.SimpleMemberMethod));

        //    // an expression to call the member method on the FakeObject instance using the FakeArgument argument
        //    var bodyEx = Expression.Call(objEx, methodInfo, argEx);

        //    // an expression referring to the ExecutionContext lambda parameter
        //    var contextParamEx = Expression.Parameter(typeof(ExecutionContext), "context");

        //    // finally, the lambda expression that uses the single lambda parameter and executes the lambda body
        //    var lambda = Expression.Lambda<Func<ExecutionContext, int>>(bodyEx, contextParamEx);

        //    // serialize both lambdas and confirm they match
        //    var expectedData = TestFixture.Anywhere.Serialize(expectedLambda);
        //    var actualData = TestFixture.Anywhere.Serialize(lambda);
        //    Assert.Equal(expectedData, actualData);

        //    // deserialize and execute both lambdas and confirm the results match
        //    var expectedMethod = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, expectedData);
        //    var expectedResult = expectedMethod.Invoke();
        //    var actualMethod = await MethodModelDeserializer.DeserializeAsync(TestFixture.Environment, actualData);
        //    var actualResult = actualMethod.Invoke();

        //    Assert.Equal(expectedResult, actualResult);
        //}
    }
}