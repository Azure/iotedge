// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class UpdateModuleStateCommandTest
    {
        [Fact]
        [Unit]
        public void TestCreateValidation()
        {
            IModule module = new Mock<IModule>().Object;
            IEntityStore<string, ModuleState> store = new Mock<IEntityStore<string, ModuleState>>().Object;
            var state = new ModuleState(0, DateTime.UtcNow);

            Assert.Throws<ArgumentNullException>(() => new UpdateModuleStateCommand(null, store, state));
            Assert.Throws<ArgumentNullException>(() => new UpdateModuleStateCommand(module, null, state));
            Assert.Throws<ArgumentNullException>(() => new UpdateModuleStateCommand(module, store, null));
            Assert.NotNull(new UpdateModuleStateCommand(module, store, state));
        }

        [Fact]
        [Unit]
        public async Task TestExecuteAsync()
        {
            // Arrange
            var module = new Mock<IModule>();
            module.SetupGet(m => m.Name).Returns("module1");

            var state = new ModuleState(0, DateTime.UtcNow);

            var store = new Mock<IEntityStore<string, ModuleState>>();
            store.Setup(s => s.Put("module1", state))
                .Returns(Task.CompletedTask);

            var cmd = new UpdateModuleStateCommand(module.Object, store.Object, state);

            // Act
            await cmd.ExecuteAsync(CancellationToken.None);

            // Assert
            module.VerifyAll();
            store.VerifyAll();
        }
    }
}
