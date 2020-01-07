// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class KubernetesCommandFactoryTest
    {
        [Fact]
        [Unit]
        public async void KubernetesCommandFactoryIsReallyBasic()
        {
            var mockModule = Mock.Of<IModule>();
            var mockModuleIdentity = Mock.Of<IModuleWithIdentity>();
            var mockRuntime = Mock.Of<IRuntimeInfo>();
            var mockCommand = Mock.Of<ICommand>(c => c.ExecuteAsync(It.IsAny<CancellationToken>()) == Task.CompletedTask);
            var kcf = new KubernetesCommandFactory();
            CancellationToken ct = CancellationToken.None;

            Assert.Equal(NullCommand.Instance, await kcf.UpdateEdgeAgentAsync(mockModuleIdentity, mockRuntime));
            Assert.Equal(NullCommand.Instance, await kcf.CreateAsync(mockModuleIdentity, mockRuntime));
            Assert.Equal(NullCommand.Instance, await kcf.UpdateAsync(mockModule, mockModuleIdentity, mockRuntime));
            Assert.Equal(NullCommand.Instance, await kcf.RemoveAsync(mockModule));
            Assert.Equal(NullCommand.Instance, await kcf.StartAsync(mockModule));
            Assert.Equal(NullCommand.Instance, await kcf.StopAsync(mockModule));
            Assert.Equal(NullCommand.Instance, await kcf.RestartAsync(mockModule));
            var newCommand = await kcf.WrapAsync(mockCommand);
            await newCommand.ExecuteAsync(ct);
            Mock.Get(mockCommand).Verify(c => c.ExecuteAsync(ct), Times.Once);
        }
    }
}
