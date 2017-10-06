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

    public class RemoveModuleStateCommandTest
    {
        [Fact]
        [Unit]
        public void TestCreateValidation()
        {
            IModule module = new Mock<IModule>().Object;
            IEntityStore<string, ModuleState> store = new Mock<IEntityStore<string, ModuleState>>().Object;

            Assert.Throws<ArgumentNullException>(() => new RemoveModuleStateCommand(null, store));
            Assert.Throws<ArgumentNullException>(() => new RemoveModuleStateCommand(module, null));
            Assert.NotNull(new RemoveModuleStateCommand(module, store));
        }

        [Fact]
        [Unit]
        public async Task TestExecuteAsync()
        {
            // Arrange
            var module = new Mock<IModule>();
            module.SetupGet(m => m.Name).Returns("module1");

            var store = new Mock<IEntityStore<string, ModuleState>>();
            store.Setup(s => s.Remove("module1")).Returns(Task.CompletedTask);

            var cmd = new RemoveModuleStateCommand(module.Object, store.Object);

            // Act
            await cmd.ExecuteAsync(CancellationToken.None);

            // Assert
            module.VerifyAll();
            store.VerifyAll();
        }
    }
}
