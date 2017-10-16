// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.commands
{
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class NullCommandFactoryTest
    {
        [Fact]
        [Unit]
        public void NullCommandFactoryAllTest()
        {
            NullCommandFactory nf = NullCommandFactory.Instance;
            var moduleIdentity = new Mock<IModuleIdentity>();
            var nm = new TestModule("null", "version_null", "null", ModuleStatus.Running, new TestConfig("null"), RestartPolicy.OnUnhealthy, new ConfigurationInfo());
            var nmn = new TestModule("next", "version_null", "null", ModuleStatus.Running, new TestConfig("null"), RestartPolicy.OnUnhealthy, new ConfigurationInfo());
            ICommand createCommand = nf.Create(new ModuleWithIdentity(nm, moduleIdentity.Object));
            ICommand pullCommand = nf.Pull(nm);
            ICommand updateCommand = nf.Update(nm, new ModuleWithIdentity(nmn, moduleIdentity.Object));
            ICommand removeCommand = nf.Remove(nm);
            ICommand startCommand = nf.Start(nm);
            ICommand stopCommand = nf.Stop(nm);
            ICommand restartCommand = nf.Restart(nm);
            ICommand wrapCommand = nf.Wrap(createCommand);

            Assert.Equal(NullCommand.Instance, createCommand);
            Assert.Equal(NullCommand.Instance, pullCommand);
            Assert.Equal(NullCommand.Instance, updateCommand);
            Assert.Equal(NullCommand.Instance, removeCommand);
            Assert.Equal(NullCommand.Instance, startCommand);
            Assert.Equal(NullCommand.Instance, stopCommand);
            Assert.Equal(NullCommand.Instance, restartCommand);
            Assert.Equal(createCommand, wrapCommand);
        }
    }
}
