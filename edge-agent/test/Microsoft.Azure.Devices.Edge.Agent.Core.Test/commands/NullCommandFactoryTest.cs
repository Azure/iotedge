// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Commands
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class NullCommandFactoryTest
    {
        [Fact]
        [Unit]
        public async void NullCommandFactoryAllTestAsync()
        {
            IDictionary<string, EnvVal> envVars = new Dictionary<string, EnvVal>();
            NullCommandFactory nf = NullCommandFactory.Instance;
            var moduleIdentity = new Mock<IModuleIdentity>();
            var runtimeInfo = Mock.Of<IRuntimeInfo>();
            var nm = new TestModule("null", "version_null", "null", ModuleStatus.Running, new TestConfig("null"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo(), envVars);
            var nmn = new TestModule("next", "version_null", "null", ModuleStatus.Running, new TestConfig("null"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo(), envVars);
            ICommand createCommand = await nf.CreateAsync(new ModuleWithIdentity(nm, moduleIdentity.Object), runtimeInfo);
            ICommand updateCommand = await nf.UpdateAsync(nm, new ModuleWithIdentity(nmn, moduleIdentity.Object), runtimeInfo);
            ICommand removeCommand = await nf.RemoveAsync(nm);
            ICommand startCommand = await nf.StartAsync(nm);
            ICommand stopCommand = await nf.StopAsync(nm);
            ICommand restartCommand = await nf.RestartAsync(nm);
            ICommand wrapCommand = await nf.WrapAsync(createCommand);

            Assert.Equal(NullCommand.Instance, createCommand);
            Assert.Equal(NullCommand.Instance, updateCommand);
            Assert.Equal(NullCommand.Instance, removeCommand);
            Assert.Equal(NullCommand.Instance, startCommand);
            Assert.Equal(NullCommand.Instance, stopCommand);
            Assert.Equal(NullCommand.Instance, restartCommand);
            Assert.Equal(createCommand, wrapCommand);
        }
    }
}
