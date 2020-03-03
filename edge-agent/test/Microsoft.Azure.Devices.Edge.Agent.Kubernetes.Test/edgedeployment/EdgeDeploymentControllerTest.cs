// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.EdgeDeployment
{
    using System.Collections.Immutable;
    using System.Threading.Tasks;
    using k8s;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class EdgeDeploymentControllerTest
    {
        const string DeviceSelector = "selector";
        const string DeviceNamespace = "a-namespace";
        static readonly ResourceName ResourceName = new ResourceName("hostname", "deviceId");

        [Fact]
        public async Task ReturnFailureWhenUnableToObtainModuleIdentities()
        {
            var client = new Mock<IKubernetes>();
            var lifecycle = new Mock<IModuleIdentityLifecycleManager>();
            lifecycle.Setup(l => l.GetModuleIdentitiesAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>()))
                .ReturnsAsync(ImmutableDictionary.Create<string, IModuleIdentity>());
            var serviceMapper = new Mock<IKubernetesServiceMapper>();
            var deploymentMapper = new Mock<IKubernetesDeploymentMapper>();
            var pvcMapper = new Mock<IKubernetesPvcMapper>();
            var serviceAccountMapper = new Mock<IKubernetesServiceAccountMapper>();
            var controller = new EdgeDeploymentController(
                ResourceName,
                DeviceSelector,
                DeviceNamespace,
                client.Object,
                lifecycle.Object,
                serviceMapper.Object,
                deploymentMapper.Object,
                pvcMapper.Object,
                serviceAccountMapper.Object);
            var module = new Mock<IModule>();
            module.SetupGet(m => m.Name).Returns("module1");
            var desiredModules = ModuleSet.Create(module.Object);
            var currentModules = ModuleSet.Empty;

            var status = await controller.DeployModulesAsync(desiredModules, currentModules);

            Assert.Equal(EdgeDeploymentStatus.Failure("Unable to obtain identities for desired modules"), status);
        }
    }
}
