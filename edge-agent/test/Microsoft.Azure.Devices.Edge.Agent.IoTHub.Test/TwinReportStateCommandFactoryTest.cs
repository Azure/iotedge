// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json.Linq;
    using Xunit;
    using CommandMethodExprFunc = System.Func<Core.IModule, Core.ICommandFactory, System.Linq.Expressions.Expression<System.Func<Core.ICommandFactory, Core.ICommand>>>;
    using TestExecutionFunc = System.Func<Core.IModule, TwinReportStateCommandFactory, Core.ICommand>;

    public class TwinReportStateCommandFactoryTest
    {
        [Fact]
        [Unit]
        public void CreateInvalidInputs()
        {
            // Arrange
            var underlyingFactory = new Mock<ICommandFactory>();
            var deviceClient = new Mock<IDeviceClient>();
            var environment = new Mock<IEnvironment>();

            // Act
            // Assert
            Assert.Throws<ArgumentNullException>(
                () => new TwinReportStateCommandFactory(null, deviceClient.Object, environment.Object)
            );
            Assert.Throws<ArgumentNullException>(
                () => new TwinReportStateCommandFactory(underlyingFactory.Object, null, environment.Object)
            );
            Assert.Throws<ArgumentNullException>(
                () => new TwinReportStateCommandFactory(underlyingFactory.Object, deviceClient.Object, null)
            );
        }

        [Fact]
        [Unit]
        public void CreateSuccess()
        {
            // Arrange
            var underlyingFactory = new Mock<ICommandFactory>();
            var deviceClient = new Mock<IDeviceClient>();
            var environment = new Mock<IEnvironment>();

            // Act
            var commandFactory = new TwinReportStateCommandFactory(
                underlyingFactory.Object, deviceClient.Object, environment.Object
            );

            // Assert
            Assert.NotNull(commandFactory);
        }

        static Mock<IModule> MakeTestModule(
            string name,
            string version = "1.0",
            string type = "docker",
            ModuleStatus status = ModuleStatus.Running)
        {
            var testModule = new Mock<IModule>();
            testModule.Setup(m => m.Name).Returns(name);
            testModule.Setup(m => m.Version).Returns(version);
            testModule.Setup(m => m.Type).Returns(type);
            testModule.Setup(m => m.Status).Returns(status);

            return testModule;
        }

        static IEnumerable<object[]> CreateTestDataForForwardTests()
        {
            var updateModule = new Mock<IModule>();

            // By default we don't expect anything to get added to the twin collection.
            void DefaultAssert(TwinCollection collection, Mock<IModule> module)
            {
                var modules = collection["modules"] as JObject;
                Assert.NotNull(modules);
                Assert.True(modules.Properties().Count(prop => prop.Name == module.Object.Name) == 1);
            }

            // For the "remove" command we expect the module to be added to the twincollection with a "null" value.
            void RemoveAssert(TwinCollection collection, Mock<IModule> module)
            {
                var modules = collection["modules"] as JObject;
                Assert.NotNull(modules);

                Assert.True(modules.Properties().Count(prop => prop.Name == module.Object.Name) == 1);
                JProperty mod = modules.Property(module.Object.Name);
                Assert.Equal(mod.Value.Type, JTokenType.Null);
            }

            // Array of tuples representing each test case. CommandMethodBeingTested is an expression
            // that is used to determine what method to call on the underlying factory object which here
            // we expect to be a mock object. This is used by Moq to define the call expectation.
            //
            // TestExecutionFunc defines which method to call on the TwinReportStateCommandFactory object.
            (
                CommandMethodExprFunc CommandMethodBeingTested,
                TestExecutionFunc TestExecutionFunc,
                Mock<IModule> TestModule,
                Action<TwinCollection, Mock<IModule>> AssertAction
            )[] testInputRecords = {
                (
                    (testModule, factory) => f => f.Create(testModule),
                    (testModule, factory) => factory.Create(testModule),
                    MakeTestModule("createModule"),
                    DefaultAssert
                ),
                (
                    (testModule, factory) => f => f.Pull(testModule),
                    (testModule, factory) => factory.Pull(testModule),
                    MakeTestModule("pullModule"),
                    DefaultAssert
                ),
                (
                    (testModule, factory) => f => f.Start(testModule),
                    (testModule, factory) => factory.Start(testModule),
                    MakeTestModule("startModule"),
                    DefaultAssert
                ),
                (
                    (testModule, factory) => f => f.Stop(testModule),
                    (testModule, factory) => factory.Stop(testModule),
                    MakeTestModule("stopModule"),
                    DefaultAssert
                ),
                (
                    (testModule, factory) => f => f.Update(testModule, updateModule.Object),
                    (testModule, factory) => factory.Update(testModule, updateModule.Object),
                    MakeTestModule("updateModule"),
                    DefaultAssert
                ),
                (
                    (testModule, factory) => f => f.Remove(testModule),
                    (testModule, factory) => factory.Remove(testModule),
                    MakeTestModule("removeModule"),
                    RemoveAssert
                )
            };

            return testInputRecords.Select(r => new object[]
            {
                r.CommandMethodBeingTested,
                r.TestExecutionFunc,
                r.TestModule,
                r.AssertAction
            });
        }

        static IModule MakeModuleFromMock(IModule mockModule)
        {
            return new TestModule(
                mockModule.Name,
                mockModule.Version,
                mockModule.Type,
                mockModule.Status,
                new TestConfig("testimage")
            );
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestDataForForwardTests))]
        public async void TestCommandExecute(
            CommandMethodExprFunc methodExpr,
            TestExecutionFunc testFunc,
            Mock<IModule> testModule,
            Action<TwinCollection, Mock<IModule>> assertAction
        )
        {
            var underlyingCommand = new Mock<ICommand>();
            underlyingCommand.Setup(c => c.ExecuteAsync(CancellationToken.None))
                .Returns(Task.CompletedTask);

            await this.TestCommand(
                methodExpr,
                testFunc,
                testModule,
                assertAction,
                underlyingCommand,
                cmd => cmd.ExecuteAsync(CancellationToken.None));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestDataForForwardTests))]
        public async void TestCommandUndo(
            CommandMethodExprFunc methodExpr,
            TestExecutionFunc testFunc,
            Mock<IModule> testModule,
            Action<TwinCollection, Mock<IModule>> assertAction
        )
        {
            var underlyingCommand = new Mock<ICommand>();
            underlyingCommand.Setup(c => c.UndoAsync(CancellationToken.None))
                .Returns(Task.CompletedTask);

            await this.TestCommand(
                methodExpr,
                testFunc,
                testModule,
                assertAction,
                underlyingCommand,
                cmd => cmd.UndoAsync(CancellationToken.None));
        }

        async Task TestCommand(
            CommandMethodExprFunc methodExpr,
            TestExecutionFunc testFunc,
            Mock<IModule> testModule,
            Action<TwinCollection, Mock<IModule>> assertAction,
            Mock<ICommand> underlyingCommand,
            Func<ICommand, Task> testAction
        )
        {
            // Arrange
            var underlyingFactory = new Mock<ICommandFactory>();
            var deviceClient = new Mock<IDeviceClient>();
            var environment = new Mock<IEnvironment>();

            underlyingFactory.Setup(methodExpr(testModule.Object, underlyingFactory.Object))
                .Returns(underlyingCommand.Object);

            ModuleSet moduleSet = ModuleSet.Empty;
            int callCount = 0;
            environment.Setup(e => e.GetModulesAsync(CancellationToken.None))
                // Return the "before" state on first invocation and the "after"
                // state on the second invocation.
                .Callback(
                    () =>
                    {
                        moduleSet = ModuleSet.Empty;
                        ++callCount;

                        // For the first call we return an empty set for all tests except
                        // for the "remove" case.
                        if (callCount == 1)
                        {
                            if (testModule.Object.Name == "removeModule")
                            {
                                moduleSet = ModuleSet.Create(MakeModuleFromMock(testModule.Object));
                            }
                        }
                        // For the second call we return a non-empty set for all tests
                        // except for the "remove" case.
                        else
                        {
                            if (testModule.Object.Name != "removeModule")
                            {
                                moduleSet = ModuleSet.Create(MakeModuleFromMock(testModule.Object));
                            }
                        }
                    })
                .Returns(() => Task.FromResult(moduleSet));

            TwinCollection collection = null;
            deviceClient.Setup(d => d.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .Callback<TwinCollection>(coll => collection = coll)
                .Returns(Task.CompletedTask);

            // Act
            var commandFactory = new TwinReportStateCommandFactory(
                underlyingFactory.Object, deviceClient.Object, environment.Object
            );
            ICommand command = testFunc(testModule.Object, commandFactory);
            await testAction(command);

            // Assert
            underlyingFactory.VerifyAll();
            underlyingCommand.VerifyAll();
            deviceClient.VerifyAll();
            environment.VerifyAll();
            Assert.NotNull(collection);
            assertAction(collection, testModule);
        }
    }
}