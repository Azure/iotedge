// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.Planners
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Planners;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;
    using Constants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesPlannerTest
    {
        const string Namespace = "namespace";
        static readonly ResourceName ResourceName = new ResourceName("hostname", "deviceId");
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly DockerConfig Config1 = new DockerConfig("image1");
        static readonly DockerConfig Config2 = new DockerConfig("image2");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly KubernetesModuleOwner EdgeletModuleOwner = new KubernetesModuleOwner("v1", "Deployment", "iotedged", "123");
        static readonly IRuntimeInfo RuntimeInfo = Mock.Of<IRuntimeInfo>();
        static readonly IKubernetes DefaultClient = Mock.Of<IKubernetes>();
        static readonly ICommandFactory DefaultCommandFactory = new KubernetesCommandFactory();
        static readonly ICombinedConfigProvider<CombinedKubernetesConfig> ConfigProvider = Mock.Of<ICombinedConfigProvider<CombinedKubernetesConfig>>();

        [Fact]
        [Unit]
        public void ConstructorThrowsOnInvalidParams()
        {
            Assert.Throws<ArgumentException>(() => new KubernetesPlanner(null, ResourceName, DefaultClient, DefaultCommandFactory, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentException>(() => new KubernetesPlanner(string.Empty, ResourceName, DefaultClient, DefaultCommandFactory, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new KubernetesPlanner(Namespace, null, DefaultClient, DefaultCommandFactory, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new KubernetesPlanner(Namespace, ResourceName, null, DefaultCommandFactory, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new KubernetesPlanner(Namespace, ResourceName, DefaultClient, null, ConfigProvider, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new KubernetesPlanner(Namespace, ResourceName, DefaultClient, DefaultCommandFactory, null, EdgeletModuleOwner));
            Assert.Throws<ArgumentNullException>(() => new KubernetesPlanner(Namespace, ResourceName, DefaultClient, DefaultCommandFactory, ConfigProvider, null));
        }

        [Fact]
        [Unit]
        public async void KubernetesPlannerNoModulesNoPlan()
        {
            Option<EdgeDeploymentStatus> reportedStatus = Option.None<EdgeDeploymentStatus>();
            var edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>(), null);
            bool getCrdCalled = false;

            using (var server = new KubernetesApiServer(
                resp: string.Empty,
                shouldNext: async httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}/{ResourceName}"))
                        {
                            getCrdCalled = true;
                            await httpContext.Response.Body.WriteAsync(JsonConvert.SerializeObject(edgeDefinition).ToBody());
                        }
                    }

                    return false;
                }))
            {
                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });
                var planner = new KubernetesPlanner(Namespace, ResourceName, client, DefaultCommandFactory, ConfigProvider, EdgeletModuleOwner);

                Plan addPlan = await planner.PlanAsync(ModuleSet.Empty, ModuleSet.Empty, RuntimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty);
                Assert.True(getCrdCalled);
                Assert.Equal(Plan.Empty, addPlan);
                Assert.False(reportedStatus.HasValue);
            }
        }

        [Fact]
        [Unit]
        public async void KubernetesPlannerPlanFailsWithNonDistinctModules()
        {
            var edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>(), null);
            bool getCrdCalled = false;

            using (var server = new KubernetesApiServer(
                resp: string.Empty,
                shouldNext: async httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}/{ResourceName}"))
                        {
                            getCrdCalled = true;
                            await httpContext.Response.Body.WriteAsync(JsonConvert.SerializeObject(edgeDefinition).ToBody());
                        }
                    }

                    return false;
                }))
            {
                IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
                IModule m2 = new DockerModule("Module1", "v1", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
                ModuleSet addRunning = ModuleSet.Create(m1, m2);

                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });
                var planner = new KubernetesPlanner(Namespace, ResourceName, client, DefaultCommandFactory, ConfigProvider, EdgeletModuleOwner);
                await Assert.ThrowsAsync<InvalidIdentityException>(
                    () => planner.PlanAsync(addRunning, ModuleSet.Empty, RuntimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty));
                Assert.True(getCrdCalled);
            }
        }

        [Fact]
        [Unit]
        public async void KubernetesPlannerPlanFailsWithNonDockerModules()
        {
            var edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>(), null);
            bool getCrdCalled = false;

            using (var server = new KubernetesApiServer(
                resp: string.Empty,
                shouldNext: async httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}/{ResourceName}"))
                        {
                            getCrdCalled = true;
                            await httpContext.Response.Body.WriteAsync(JsonConvert.SerializeObject(edgeDefinition).ToBody());
                        }
                    }

                    return false;
                }))
            {
                IModule m1 = new NonDockerModule("module1", "v1", "unknown", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars, string.Empty);
                ModuleSet addRunning = ModuleSet.Create(m1);

                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });
                var planner = new KubernetesPlanner(Namespace, ResourceName, client, DefaultCommandFactory, ConfigProvider, EdgeletModuleOwner);
                await Assert.ThrowsAsync<InvalidModuleException>(() => planner.PlanAsync(addRunning, ModuleSet.Empty, RuntimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty));
                Assert.True(getCrdCalled);
            }
        }

        [Fact]
        [Unit]
        public async void KubernetesPlannerPlanExistsWhenNoChanges()
        {
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            IModule m2 = new DockerModule("module2", "v1", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            KubernetesConfig kc1 = new KubernetesConfig("image1", CreatePodParameters.Create(), Option.None<AuthConfig>());
            KubernetesConfig kc2 = new KubernetesConfig("image2", CreatePodParameters.Create(), Option.None<AuthConfig>());
            var edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>() { new KubernetesModule(m1, kc1, EdgeletModuleOwner), new KubernetesModule(m2, kc2, EdgeletModuleOwner) }, null);
            bool getCrdCalled = false;

            using (var server = new KubernetesApiServer(
                resp: string.Empty,
                shouldNext: async httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}/{ResourceName}"))
                        {
                            getCrdCalled = true;
                            await httpContext.Response.Body.WriteAsync(JsonConvert.SerializeObject(edgeDefinition, EdgeDeploymentSerialization.SerializerSettings).ToBody());
                        }
                    }

                    return false;
                }))
            {
                ModuleSet desired = ModuleSet.Create(m1, m2);
                ModuleSet current = ModuleSet.Create(m1, m2);

                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });
                var planner = new KubernetesPlanner(Namespace, ResourceName, client, DefaultCommandFactory, ConfigProvider, EdgeletModuleOwner);
                var plan = await planner.PlanAsync(desired, current, RuntimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty);
                Assert.True(getCrdCalled);
                Assert.Single(plan.Commands);
                Assert.True(plan.Commands.First() is EdgeDeploymentCommand);
            }
        }

        [Fact]
        [Unit]
        public async void KubernetesPlannerPlanExistsWhenChangesMade()
        {
            var edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>(), null);
            bool getCrdCalled = false;

            using (var server = new KubernetesApiServer(
                resp: string.Empty,
                shouldNext: async httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}/{ResourceName}"))
                        {
                            getCrdCalled = true;
                            await httpContext.Response.Body.WriteAsync(JsonConvert.SerializeObject(edgeDefinition).ToBody());
                        }
                    }

                    return false;
                }))
            {
                IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
                IModule m2 = new DockerModule("module2", "v1", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
                ModuleSet desired = ModuleSet.Create(m1);
                ModuleSet current = ModuleSet.Create(m2);

                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });
                var planner = new KubernetesPlanner(Namespace, ResourceName, client, DefaultCommandFactory, ConfigProvider, EdgeletModuleOwner);
                var plan = await planner.PlanAsync(desired, current, RuntimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty);
                Assert.True(getCrdCalled);
                Assert.Single(plan.Commands);
                Assert.True(plan.Commands.First() is EdgeDeploymentCommand);
            }
        }

        [Fact]
        [Unit]
        public async void KubernetesPlannerPlanExistsWhenDeploymentsQueryFails()
        {
            bool getCrdCalled = false;

            using (var server = new KubernetesApiServer(
                resp: string.Empty,
                shouldNext: httpContext =>
                {
                    string pathStr = httpContext.Request.Path.Value;
                    string method = httpContext.Request.Method;
                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pathStr.Contains($"namespaces/{Namespace}/{Constants.EdgeDeployment.Plural}/{ResourceName}"))
                        {
                            getCrdCalled = true;
                            httpContext.Response.StatusCode = 404;
                        }
                    }

                    return Task.FromResult(false);
                }))
            {
                IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
                IModule m2 = new DockerModule("module2", "v1", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
                ModuleSet desired = ModuleSet.Create(m1);
                ModuleSet current = ModuleSet.Create(m2);

                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });
                var planner = new KubernetesPlanner(Namespace, ResourceName, client, DefaultCommandFactory, ConfigProvider, EdgeletModuleOwner);
                var plan = await planner.PlanAsync(desired, current, RuntimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty);
                Assert.True(getCrdCalled);
                Assert.Single(plan.Commands);
                Assert.True(plan.Commands.First() is EdgeDeploymentCommand);
            }
        }

        [Fact]
        [Unit]
        public async void KubernetesPlannerShutdownTest()
        {
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            ModuleSet current = ModuleSet.Create(m1);

            var planner = new KubernetesPlanner(Namespace, ResourceName, DefaultClient, DefaultCommandFactory, ConfigProvider, EdgeletModuleOwner);
            var plan = await planner.CreateShutdownPlanAsync(current);
            Assert.Equal(Plan.Empty, plan);
        }

        class NonDockerModule : IModule<string>
        {
            public NonDockerModule(string name, string version, string type, ModuleStatus desiredStatus, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy restartPolicy, ImagePullPolicy imagePullPolicy, ConfigurationInfo configurationInfo, IDictionary<string, EnvVal> env, string config)
            {
                this.Name = name;
                this.Version = version;
                this.Type = type;
                this.DesiredStatus = desiredStatus;
                this.RestartPolicy = restartPolicy;
                this.ImagePullPolicy = imagePullPolicy;
                this.ConfigurationInfo = configurationInfo;
                this.Env = env;
                this.Config = config;
            }

            public bool Equals(IModule other) => throw new NotImplementedException();

            public string Name { get; set; }

            public string Version { get; }

            public string Type { get; }

            public ModuleStatus DesiredStatus { get; }

            public global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy RestartPolicy { get; }

            public ImagePullPolicy ImagePullPolicy { get; }

            public ConfigurationInfo ConfigurationInfo { get; }

            public IDictionary<string, EnvVal> Env { get; }

            public bool IsOnlyModuleStatusChanged(IModule other) => throw new NotImplementedException();

            public bool Equals(IModule<string> other) => throw new NotImplementedException();

            public string Config { get; }
        }
    }
}
