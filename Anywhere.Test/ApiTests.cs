using DidoNet.Test.Common;
using DidoNet.TestLib;
using DidoNet.TestLibDependency;
using System;
using Xunit;

namespace DidoNet.Test
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

            var actualResult = await Dido.DebugRunLocalAsync((context) => testObject.MemberMethodWithDependency(testModel), TestFixture.Configuration);

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

            var actualResult = await Dido.ReleaseRunLocalAsync((context) => testObject.MemberMethodWithDependency(testModel), TestFixture.Configuration);

            Assert.Equal(expectedResult, actualResult);
        }

        // NOTE: Anywhere.RemoteExecuteAsync is tested in the AnywhereNET.Test.Runner project.
    }
}