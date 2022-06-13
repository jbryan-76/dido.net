using DidoNet.Test.Common;
using DidoNet.TestLib;
using DidoNet.TestLibDependency;
using System;
using System.Threading;
using Xunit;

namespace DidoNet.Test
{
    public class ApiTests : IClassFixture<TestFixture>
    {
        readonly TestFixture TestFixture;

        public ApiTests(TestFixture fixture)
        {
            TestFixture = fixture;
        }

        /// <summary>
        /// Implicitly tests Dido.RunLocalAsync when used in Debug mode.
        /// </summary>
        [Fact]
        public async void DebugRunLocal()
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

            using (var source = new CancellationTokenSource())
            {
                var actualResult = await Dido.DebugRunLocalAsync(
                    (context) => testObject.MemberMethodWithDependency(testModel),
                    TestFixture.Configuration,
                    source.Token);
                Assert.Equal(expectedResult, actualResult);
            }
        }

        /// <summary>
        /// Implicitly tests canceling Dido.RunLocalAsync when used in Debug mode.
        /// </summary>
        [Fact]
        public async void DebugRunLocalWithCancel()
        {
            // create a cancellation source and timer to cancel the task after 1 second
            using (var source = new CancellationTokenSource())
            using (var timer = new Timer((_) => source.Cancel(), null, 1000, 0))
            {
                // execute a busy loop and confirm it throws OperationCanceledException
                await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    var result = await Dido.DebugRunLocalAsync<bool>(
                        (context) => SampleWorkerClass.InfiniteLoopWithCancellation(context.Cancel),
                        TestFixture.Configuration,
                        source.Token);
                });
            }
        }

        /// <summary>
        /// Implicitly tests timing out Dido.RunLocalAsync when used in Debug mode.
        /// </summary>
        [Fact]
        public async void DebugRunLocalWithTimeout()
        {
            using (var source = new CancellationTokenSource())
            {
                // use the test fixture configuration updated to timeout after a half second
                var configuration = new Configuration
                {
                    MaxTries = TestFixture.Configuration.MaxTries,
                    TimeoutInMs = 500,
                    RunnerUri = TestFixture.Configuration.RunnerUri,
                    ExecutionMode = TestFixture.Configuration.ExecutionMode,
                    ResolveLocalAssemblyAsync = TestFixture.Configuration.ResolveLocalAssemblyAsync
                };

                // execute a busy loop and confirm it throws TimeoutException
                await Assert.ThrowsAsync<TimeoutException>(async () =>
                {
                    var result = await Dido.DebugRunLocalAsync<bool>(
                        (context) => SampleWorkerClass.InfiniteLoopWithCancellation(context.Cancel),
                        configuration,
                        source.Token);
                });
            }
        }

        /// <summary>
        /// Implicitly tests Dido.RunLocalAsync when used in Release mode.
        /// </summary>
        [Fact]
        public async void ReleaseRunLocal()
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

            using (var source = new CancellationTokenSource())
            {
                var actualResult = await Dido.ReleaseRunLocalAsync(
                    (context) => testObject.MemberMethodWithDependency(testModel),
                    TestFixture.Configuration,
                    source.Token);

                Assert.Equal(expectedResult, actualResult);
            }
        }

        /// <summary>
        /// Implicitly tests canceling Dido.RunLocalAsync when used in Release mode.
        /// </summary>
        [Fact]
        public async void ReleaseRunLocalWithCancel()
        {
            // create a cancellation source and timer to cancel the task after 1 second
            using (var source = new CancellationTokenSource())
            using (var timer = new Timer((_) => source.Cancel(), null, 1000, 0))
            {
                // execute a busy loop and confirm it throws OperationCanceledException
                await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    var result = await Dido.ReleaseRunLocalAsync<bool>(
                        (context) => SampleWorkerClass.InfiniteLoopWithCancellation(context.Cancel),
                        TestFixture.Configuration,
                        source.Token);
                });
            }
        }

        /// <summary>
        /// Implicitly tests timing out Dido.RunLocalAsync when used in Release mode.
        /// </summary>
        [Fact]
        public async void ReleaseRunLocalWithTimeout()
        {
            using (var source = new CancellationTokenSource())
            {
                // use the test fixture configuration updated to timeout after a half second
                var configuration = new Configuration
                {
                    MaxTries = TestFixture.Configuration.MaxTries,
                    TimeoutInMs = 500,
                    RunnerUri = TestFixture.Configuration.RunnerUri,
                    ExecutionMode = TestFixture.Configuration.ExecutionMode,
                    ResolveLocalAssemblyAsync = TestFixture.Configuration.ResolveLocalAssemblyAsync
                };

                // execute a busy loop and confirm it throws TimeoutException
                await Assert.ThrowsAsync<TimeoutException>(async () =>
                {
                    var result = await Dido.ReleaseRunLocalAsync<bool>(
                        (context) => SampleWorkerClass.InfiniteLoopWithCancellation(context.Cancel),
                        configuration,
                        source.Token);
                });
            }
        }

        // NOTE: Dido.RunRemoteAsync is tested in the DidoNet.Test.Runner project.
    }
}