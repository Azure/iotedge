// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.commands
{
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class NullCommandFactoryTest
    {
        [Fact]
        [Unit]
        public void NullCommandFactoryAllTest()
        {
            NullCommandFactory nf = NullCommandFactory.Instance;
            TestModule nm = new TestModule("null","version_null","null",ModuleStatus.Running, new TestConfig("null"));
            TestModule nmn = new TestModule("next","version_null","null",ModuleStatus.Running, new TestConfig("null"));
            ICommand createCommand = nf.Create(nm);
            ICommand pullCommand = nf.Pull(nm);
            ICommand updateCommand = nf.Update(nm,nmn);
            ICommand removeCommand = nf.Remove(nm);
            ICommand startCommand = nf.Start(nm);
            ICommand stopCommand = nf.Stop(nm);

            Assert.Equal(NullCommand.Instance, createCommand);
            Assert.Equal(NullCommand.Instance, pullCommand);
            Assert.Equal(NullCommand.Instance, updateCommand);
            Assert.Equal(NullCommand.Instance, removeCommand);
            Assert.Equal(NullCommand.Instance, startCommand);
            Assert.Equal(NullCommand.Instance, stopCommand);
        }
    }
}
