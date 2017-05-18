// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class AgentTests
    {
        [Fact]
        [Unit]
        public void AgentConstructorInvalidArgs()
        {
            ModuleSet testSet = ModuleSet.Empty;
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();

            Assert.Throws<ArgumentNullException>(() => new Agent(null, mockEnvironment.Object, mockPlanner.Object));
            Assert.Throws<ArgumentNullException>(() => new Agent(testSet, null, mockPlanner.Object));
            Assert.Throws<ArgumentNullException>(() => new Agent(testSet, mockEnvironment.Object, null));
        }

        [Fact]
        [Unit]
        public async void CreateAsyncCreatesAnAgent()
        {
            ModuleSet testSet = ModuleSet.Empty;

            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();

            mockConfigSource.Setup(cs => cs.GetConfigAsync())
                .ReturnsAsync(testSet);

            Agent agent = await Agent.CreateAsync(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object);

            Assert.NotNull(agent);

        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncOnEmptyPlan()
        {
            var token = new CancellationToken();

            ModuleSet desiredSet = ModuleSet.Empty;
            ModuleSet currentSet = ModuleSet.Empty;

            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();

            mockConfigSource.Setup(cs => cs.GetConfigAsync())
                .ReturnsAsync(desiredSet);
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(currentSet);
            mockPlanner.Setup(pl => pl.Plan(desiredSet, currentSet))
                .Returns(Plan.Empty);

            Agent agent = await Agent.CreateAsync(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object);

            await agent.ReconcileAsync(token);

            mockEnvironment.Verify(env => env.GetModulesAsync(token), Times.Once);
            mockPlanner.Verify(pl => pl.Plan(desiredSet, currentSet), Times.Once);
        }


        [Fact]
        [Unit]
        public async void ReconcileAsyncOnSetPlan()
        {
            var desiredModule = new TestModule("desired", "v1", "test", ModuleStatus.Running, new TestConfig("image"));
            var currentModule = new TestModule("current", "v1", "test", ModuleStatus.Running, new TestConfig("image"));
            Option<TestPlanRecorder> recordKeeper = Option.Some(new TestPlanRecorder());
            var moduleExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestCreate, desiredModule),
                new TestRecordType(TestCommandType.TestRemove, currentModule)

            };
            var commandList = new List<ICommand>
            {
                new TestCommand(TestCommandType.TestCreate,desiredModule, recordKeeper),
                new TestCommand(TestCommandType.TestRemove, currentModule, recordKeeper)
            };
            var testPlan = new Plan(commandList);

            var token = new CancellationToken();

            ModuleSet desiredSet = ModuleSet.Create(desiredModule);
            ModuleSet currentSet = ModuleSet.Create(currentModule);
            

            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();

            mockConfigSource.Setup(cs => cs.GetConfigAsync())
                .ReturnsAsync(desiredSet);
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(currentSet);
            mockPlanner.Setup(pl => pl.Plan(desiredSet, currentSet))
                .Returns(testPlan);

            Agent agent = await Agent.CreateAsync(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object);

            await agent.ReconcileAsync(token);

            mockEnvironment.Verify(env => env.GetModulesAsync(token), Times.Once);
            mockPlanner.Verify(pl => pl.Plan(desiredSet, currentSet), Times.Once);
            recordKeeper.ForEach(r => Assert.Equal(moduleExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void ApplyDiffSuccess()
        {
            //public async Task ApplyDiffAsync(Diff diff, CancellationToken token)
            var desiredModule = new TestModule("desired", "v1", "test", ModuleStatus.Running, new TestConfig("image"));
            var currentModule = new TestModule("current", "v1", "test", ModuleStatus.Running, new TestConfig("image"));
            var cancellationTokenSource = new CancellationTokenSource();
            var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            ModuleSet desiredSet = ModuleSet.Create(desiredModule);
            ModuleSet currentSet = ModuleSet.Create(currentModule);
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();

            mockConfigSource.Setup(cs => cs.GetConfigAsync())
                .ReturnsAsync(desiredSet);

            Agent agent = await Agent.CreateAsync(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object);

            await Task
                .WhenAll(agent.ApplyDiffAsync(Diff.Empty, timeoutTokenSource.Token), 
                         agent.ApplyDiffAsync(desiredSet.Diff(currentSet), cancellationTokenSource.Token));
        }
    }
}
