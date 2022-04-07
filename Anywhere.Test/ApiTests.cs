using AnywhereNET.Test.Common;
using AnywhereNET.TestLib;
using AnywhereNET.TestLibDependency;
using System;
using Xunit;

namespace AnywhereNET.Test
{
    public class ApiTests : IClassFixture<AnywhereTestFixture>
    {
        readonly AnywhereTestFixture TestFixture;

        public ApiTests(AnywhereTestFixture fixture)
        {
            TestFixture = fixture;
        }

        /// <summary>
        /// Implicitly tests Anywhere.LocalExecuteAsync when used in Debug mode.
        /// </summary>
        [Fact]
        public async void DebugLocalExecute()
        {
            var testModel = new SampleDependencyClass
            {
                MyString = "my string",
                MyModel = new SampleDependencyModel
                {
                    MyBool = true,
                    MyInt = 456,
                    MyDateTimeOffset = DateTimeOffset.Now,
                }
            };

            var testObject = new SampleWorkerClass();

            var expectedResult = testObject.MemberMethodWithDependency(testModel);

            var actualResult = await TestFixture.Anywhere.DebugLocalExecuteAsync((context) => testObject.MemberMethodWithDependency(testModel));
            
            Assert.Equal(expectedResult, actualResult);
        }

        /// <summary>
        /// Implicitly tests Anywhere.LocalExecuteAsync when used in Release mode.
        /// </summary>
        [Fact]
        public async void ReleaseLocalExecute()
        {
            var testModel = new SampleDependencyClass
            {
                MyString = "my string",
                MyModel = new SampleDependencyModel
                {
                    MyBool = true,
                    MyInt = 456,
                    MyDateTimeOffset = DateTimeOffset.Now,
                }
            };

            var testObject = new SampleWorkerClass();

            var expectedResult = testObject.MemberMethodWithDependency(testModel);

            var actualResult = await TestFixture.Anywhere.ReleaseLocalExecuteAsync((context) => testObject.MemberMethodWithDependency(testModel));

            Assert.Equal(expectedResult, actualResult);
        }

        // NOTE: Anywhere.RemoteExecuteAsync is tested in the AnywhereNET.TestEnv project.
    }
}