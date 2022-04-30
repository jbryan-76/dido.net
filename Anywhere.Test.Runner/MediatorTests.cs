using System;
using Xunit;

namespace DidoNet.Test.Runner
{
    public class MediatorTests
    {
        [Fact]
        public void NextAvailableRunner_NotAvailable()
        {
            var mediator = new MediatorServer();

            // not available: starting
            mediator.RunnerPool.Add(new MediatorServer.Runner()
            {
                MaxTasks = 1,
                State = RunnerStates.Starting
            });

            // not available: max tasks is zero
            mediator.RunnerPool.Add(new MediatorServer.Runner()
            {
                MaxTasks = 0,
                State = RunnerStates.Ready
            });

            // not available: paused
            mediator.RunnerPool.Add(new MediatorServer.Runner()
            {
                MaxTasks = 1,
                State = RunnerStates.Paused
            });

            // not available: stopping
            mediator.RunnerPool.Add(new MediatorServer.Runner()
            {
                MaxTasks = 1,
                State = RunnerStates.Stopping
            });

            // not available: no available task slots, and no queue
            mediator.RunnerPool.Add(new MediatorServer.Runner()
            {
                MaxTasks = 1,
                ActiveTasks = 1,
                MaxQueue = 0,
                State = RunnerStates.Ready
            });

            // not available: no available task slots, and queue is full
            mediator.RunnerPool.Add(new MediatorServer.Runner()
            {
                MaxTasks = 1,
                ActiveTasks = 1,
                MaxQueue = 1,
                QueueLength = 1,
                State = RunnerStates.Ready
            });

            var runner = mediator.GetNextAvailableRunner(new RunnerRequestMessage());
            Assert.Null(runner);
        }

        [Fact]
        public void NextAvailableRunner_ByPlatform()
        {
            var mediator = new MediatorServer();

            var linux = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                Platform = OSPlatforms.Linux,
                MaxTasks = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(linux);

            var windows = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                Platform = OSPlatforms.Windows,
                MaxTasks = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(windows);

            var osx = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                Platform = OSPlatforms.OSX,
                MaxTasks = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(osx);

            var runner = mediator.GetNextAvailableRunner(new RunnerRequestMessage
            {
                Platform = OSPlatforms.Windows
            });

            Assert.NotNull(runner);
            Assert.Equal(windows.Label, runner!.Label);
        }

        [Fact]
        public void NextAvailableRunner_ByLabel()
        {
            var mediator = new MediatorServer();

            var one = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(one);

            var two = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(two);

            var three = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(three);

            var runner = mediator.GetNextAvailableRunner(new RunnerRequestMessage
            {
                Label = two.Label
            });

            Assert.NotNull(runner);
            Assert.Equal(two.Label, runner!.Label);
        }

        [Fact]
        public void NextAvailableRunner_ByTags()
        {
            var mediator = new MediatorServer();

            var one = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                Tags = new string[] { "red", "blue" },
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(one);

            var two = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                Tags = new string[] { "green", "black" },
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(two);

            var three = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                Tags = new string[] { "yellow", "orange" },
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(three);

            var runner = mediator.GetNextAvailableRunner(new RunnerRequestMessage
            {
                Tags = new string[] { "red", }
            });
            Assert.NotNull(runner);
            Assert.Equal(one.Label, runner!.Label);

            runner = mediator.GetNextAvailableRunner(new RunnerRequestMessage
            {
                Tags = new string[] { "blue", }
            });
            Assert.NotNull(runner);
            Assert.Equal(one.Label, runner!.Label);

            runner = mediator.GetNextAvailableRunner(new RunnerRequestMessage
            {
                Tags = new string[] { "green", }
            });
            Assert.NotNull(runner);
            Assert.Equal(two.Label, runner!.Label);
        }

        [Fact]
        public void NextAvailableRunner_ByAvailableTasks()
        {
            var mediator = new MediatorServer();

            // not available: all task slots occupied
            var one = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                ActiveTasks = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(one);

            // available: available task slot
            var two = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                ActiveTasks = 0,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(two);

            // not available: all task slots occupied
            var three = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                ActiveTasks = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(three);

            var runner = mediator.GetNextAvailableRunner(new RunnerRequestMessage());

            Assert.NotNull(runner);
            Assert.Equal(two.Label, runner!.Label);
        }

        [Fact]
        public void NextAvailableRunner_ByAvailableQueue()
        {
            var mediator = new MediatorServer();

            // not available: all task slots occupied and queue full
            var one = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                ActiveTasks = 1,
                MaxQueue = 1,
                QueueLength = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(one);

            // available: available queue
            var two = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                ActiveTasks = 1,
                MaxQueue = 1,
                QueueLength = 0,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(two);

            // not available: all task slots occupied and queue full
            var three = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                ActiveTasks = 1,
                MaxQueue = 1,
                QueueLength = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(three);

            var runner = mediator.GetNextAvailableRunner(new RunnerRequestMessage());

            Assert.NotNull(runner);
            Assert.Equal(two.Label, runner!.Label);
        }

        [Fact]
        public void NextAvailableRunner_ByUnlimitedQueue()
        {
            var mediator = new MediatorServer();

            // not available: all task slots occupied and no queue
            var one = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                ActiveTasks = 1,
                MaxQueue = 0,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(one);

            // available: unlimited queue
            var two = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                ActiveTasks = 1,
                MaxQueue = -1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(two);

            // not available: all task slots occupied and queue full
            var three = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                ActiveTasks = 1,
                MaxQueue = 1,
                QueueLength = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(three);

            var runner = mediator.GetNextAvailableRunner(new RunnerRequestMessage());

            Assert.NotNull(runner);
            Assert.Equal(two.Label, runner!.Label);
        }

        [Fact]
        public void NextAvailableRunner_BySlotPriority()
        {
            var mediator = new MediatorServer();

            // available: least open slots
            var one = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 3,
                ActiveTasks = 2,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(one);

            // most available: most open slots
            var two = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 4,
                ActiveTasks = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(two);

            // available: some open slots
            var three = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 4,
                ActiveTasks = 2,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(three);

            var runner = mediator.GetNextAvailableRunner(new RunnerRequestMessage());

            Assert.NotNull(runner);
            Assert.Equal(two.Label, runner!.Label);
        }

        [Fact]
        public void NextAvailableRunner_ByQueuePriority()
        {
            var mediator = new MediatorServer();

            // available: medium queue
            var one = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                ActiveTasks = 1,
                MaxQueue = 10,
                QueueLength = 5,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(one);

            // most available: smallest queue
            var two = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                ActiveTasks = 1,
                MaxQueue = -1,
                QueueLength = 1,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(two);

            // available: biggest queue
            var three = new MediatorServer.Runner()
            {
                Label = Guid.NewGuid().ToString(),
                MaxTasks = 1,
                ActiveTasks = 1,
                MaxQueue = 10,
                QueueLength = 8,
                State = RunnerStates.Ready
            };
            mediator.RunnerPool.Add(three);

            var runner = mediator.GetNextAvailableRunner(new RunnerRequestMessage());

            Assert.NotNull(runner);
            Assert.Equal(two.Label, runner!.Label);
        }
    }
}