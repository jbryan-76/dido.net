using DidoNet.Test.Common;
using DidoNet.TestLib;
using DidoNet.TestLibDependency;
using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;

namespace DidoNet.Test
{
    public class SerializationAndInvocationTests : IClassFixture<AnywhereTestFixture>
    {
        readonly AnywhereTestFixture TestFixture;

        public SerializationAndInvocationTests(AnywhereTestFixture fixture)
        {
            TestFixture = fixture;
        }

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
        /// to be deserialized and executed by the Anywhere.Test.Runner project.
        /// <para/>
        /// NOTE this unit test must be run before any test from Anywhere.Test.Runner.DeserializationAndInvocationTests.
        /// </summary>
        [Fact]
        public async void GenerateSerializedMethodInvocationData()
        {
            // serialize a lambda expression invoking a member method
            FakeArgument = 111;
            var bytes = await Dido.SerializeAsync((context) => FakeObject.SimpleMemberMethod(FakeArgument));
            // save the serialized model
            var path = Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.MemberMethodFile);
            File.WriteAllBytes(path, bytes);
            // save the expected result
            var result = FakeObject.SimpleMemberMethod(FakeArgument);
            path = Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.MemberResultFile);
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(result));

            // serialize a lambda expression invoking a static method
            FakeArgument = 222;
            bytes = await Dido.SerializeAsync((context) => SampleWorkerClass.SimpleStaticMethod(FakeArgument));
            // save the serialized model
            path = Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.StaticMethodFile);
            File.WriteAllBytes(path, bytes);
            // save the expected result
            result = SampleWorkerClass.SimpleStaticMethod(FakeArgument);
            path = Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.StaticResultFile);
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(result));

            // serialize a lambda expression that uses dependent assemblies
            FakeArgument = 333;
            DependencyModel.MyString = "test";
            DependencyModel.MyModel = new SampleDependencyModel
            {
                MyBool = false,
                MyDateTimeOffset = new DateTimeOffset(new DateTime(2000, 2, 2, 2, 2, 2)),
                MyInt = 42
            };
            bytes = await Dido.SerializeAsync((context) => FakeObject.MemberMethodWithDependency(DependencyModel));
            // save the serialized model
            path = Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.DependencyMethodFile);
            File.WriteAllBytes(path, bytes);
            // save the expected result
            var dependencyResult = FakeObject.MemberMethodWithDependency(DependencyModel);
            path = Path.Combine(TestFixture.SharedTestDataPath, AnywhereTestFixture.DependencyResultFile);
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(dependencyResult));
        }

        /// <summary>
        /// This test verifies a lambda invoking a member method on unit test members is properly 
        /// (de)serialized and invoked.
        /// </summary>
        [Fact]
        public async void TestMemberMethod()
        {
            // set up the test objects
            FakeArgument = 456;

            // serialize, deserialize, execute, and verify
            var data = await Dido.SerializeAsync((context) => FakeObject.SimpleMemberMethod(FakeArgument));
            var lambda = await Dido.DeserializeAsync<int>(data, TestFixture.Environment);
            var result = lambda.Invoke(TestFixture.Environment.ExecutionContext);
            Assert.Equal(FakeArgument, result);
        }

        /// <summary>
        /// This test verifies a lambda invoking a member method on a local closure object is properly 
        /// (de)serialized and invoked.
        /// </summary>
        [Fact]
        public async void TestLocalObjects()
        {
            // set up the test objects
            var obj = new SampleWorkerClass();
            int arg = 123;

            // serialize, deserialize, execute, and verify
            var data = await Dido.SerializeAsync((context) => obj.SimpleMemberMethod(arg));
            var lambda = await Dido.DeserializeAsync<int>(data, TestFixture.Environment);
            var result = lambda.Invoke(TestFixture.Environment.ExecutionContext);
            Assert.Equal(arg, result);
        }

        /// <summary>
        /// This test verifies (de)serialization and execution using json as the data format.
        /// </summary>
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
            Node receivedModel;
            using (var stream = new MemoryStream())
            {
                var settings = new ExpressionSerializeSettings
                {
                    Format = ExpressionSerializeSettings.Formats.Json
                };
                ExpressionSerializer.Serialize(transmittedModel, stream, settings);

                var bytes = stream.ToArray();

                stream.Position = 0;
                receivedModel = ExpressionSerializer.Deserialize(stream, settings);
            }

            // decode the model back to a lambda expression and execute it
            var lambda = await ExpressionSerializer.DecodeAsync<object>(receivedModel, TestFixture.Environment);
            var result = lambda.Invoke(TestFixture.Environment.ExecutionContext);

            // confirm the result
            Assert.Equal(expectedResult, result);
        }

        /// <summary>
        /// This test verifies (de)serialization and execution using bson as the data format.
        /// </summary>
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
                var settings = new ExpressionSerializeSettings
                {
                    Format = ExpressionSerializeSettings.Formats.Bson
                };

                // serialize a lambda expression
                await ExpressionSerializer.SerializeAsync((context) => ((SampleWorkerClass)obj).SimpleMemberMethod(arg), stream, settings);

                // deserialize it
                stream.Position = 0;
                var lambda = await ExpressionSerializer.DeserializeAsync<object>(stream, TestFixture.Environment, settings);

                // execute it
                var result = lambda.Invoke(TestFixture.Environment.ExecutionContext);

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
            var data = await Dido.SerializeAsync((context) => SampleWorkerClass.SimpleStaticMethod(FakeArgument));
            var lambda = await Dido.DeserializeAsync<int>(data, TestFixture.Environment);
            var result = lambda.Invoke(TestFixture.Environment.ExecutionContext);

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
            var data = await Dido.SerializeAsync((context) => FakeObject.MemberMethodWithDependency(depModel));
            var lambda = await Dido.DeserializeAsync<string>(data, TestFixture.Environment);
            var actualResult = lambda.Invoke(TestFixture.Environment.ExecutionContext);
            var expectedResult = FakeObject.MemberMethodWithDependency(depModel);
            Assert.Equal(expectedResult, actualResult);
        }

        /// <summary>
        /// This test verifies a lambda using code with additional assembly dependencies is properly 
        /// (de)serialized and invoked.
        /// </summary>
        [Fact]
        public async void TestMemberMethodWithDependency()
        {
            var data = await Dido.SerializeAsync((context) => FakeObject.MemberMethodWithDependency(DependencyModel));
            var lambda = await Dido.DeserializeAsync<string>(data, TestFixture.Environment);
            var actualResult = lambda.Invoke(TestFixture.Environment.ExecutionContext);
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

            var data = await Dido.SerializeAsync((context) => Foo(context, 23, closureVal));
            var lambda = await Dido.DeserializeAsync<string>(data, TestFixture.Environment);
            var actualResult = lambda.Invoke(TestFixture.Environment.ExecutionContext);
            var expectedResult = Foo(TestFixture.Environment.ExecutionContext, 23, closureVal);
            Assert.Equal(expectedResult, actualResult);
        }

        /// <summary>
        /// This test verifies an explicitly created lambda expression matches an equivalent
        /// lambda expression in code. This is critical to confirm some tests in Anywhere.Test.Runner
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
            var expectedData2 = await Dido.SerializeAsync(expectedLambda);
            var actualData2 = await Dido.SerializeAsync(lambda);
            Assert.True(System.Linq.Enumerable.SequenceEqual(expectedData2, actualData2));

            // deserialize and execute both lambdas and confirm the results match
            var expectedMethod2 = await Dido.DeserializeAsync<int>(expectedData2, TestFixture.Environment);
            var expectedResult2 = expectedMethod2.Invoke(TestFixture.Environment.ExecutionContext);
            var actualMethod2 = await Dido.DeserializeAsync<int>(actualData2, TestFixture.Environment);
            var actualResult2 = actualMethod2.Invoke(TestFixture.Environment.ExecutionContext);
            Assert.Equal(expectedResult2, actualResult2);
        }
    }
}