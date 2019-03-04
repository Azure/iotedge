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

    public class AddToStoreCommandTest
    {
        [Fact]
        [Unit]
        public void TestCreateValidation()
        {
            IEntityStore<string, ModuleState> store = new Mock<IEntityStore<string, ModuleState>>().Object;
            var state = new ModuleState(0, DateTime.UtcNow);

            Assert.Throws<ArgumentException>(() => new AddToStoreCommand<ModuleState>(store, null, state));
            Assert.Throws<ArgumentNullException>(() => new AddToStoreCommand<ModuleState>(null, "foo", state));
            Assert.Throws<ArgumentNullException>(() => new AddToStoreCommand<ModuleState>(store, "foo", null));
            Assert.NotNull(new AddToStoreCommand<ModuleState>(store, "foo", state));
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

            var cmd = new AddToStoreCommand<ModuleState>(store.Object, module.Object.Name, state);

            // Act
            await cmd.ExecuteAsync(CancellationToken.None);

            // Assert
            module.VerifyAll();
            store.VerifyAll();
        }
    }
}
