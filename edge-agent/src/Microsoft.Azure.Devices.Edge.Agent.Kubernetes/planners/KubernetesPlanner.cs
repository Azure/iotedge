// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Planners
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using k8s;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Commands;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes;

    public class KubernetesPlanner<T> : IPlanner
    {
        readonly IKubernetes client;
        readonly ICommandFactory commandFactory;
        readonly ICombinedConfigProvider<T> combinedConfigProvider;
        readonly string iotHubHostname;
        readonly string gatewayHostname;
        readonly string deviceId;

        public KubernetesPlanner(
            string iotHubHostname,
            string gatewayHostname,
            string deviceId,
            IKubernetes client,
            ICommandFactory commandFactory,
            ICombinedConfigProvider<T> combinedConfigProvider

        )
        {
            this.iotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.gatewayHostname = Preconditions.CheckNonWhiteSpace(gatewayHostname, nameof(gatewayHostname));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.commandFactory = Preconditions.CheckNotNull(commandFactory, nameof(commandFactory));
            this.combinedConfigProvider = Preconditions.CheckNotNull(combinedConfigProvider, nameof(combinedConfigProvider));
        }

        public async Task<Plan> PlanAsync(
            ModuleSet desired,
            ModuleSet current,
            IRuntimeInfo runtimeInfo,
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities)
        {
            Events.LogDesired(desired);
            Events.LogCurrent(current);
            Events.LogIdentities(moduleIdentities.Values);

            var k8sModules = new List<KubernetesModule>();
            foreach (var m in desired.Modules)
            {
                if (moduleIdentities.TryGetValue(m.Key, out IModuleIdentity moduleIdentity))
                {
                    KubernetesModule moduleWithIdentity;
                    IdentityProviderServiceCredentials creds;
                    if (moduleIdentity.Credentials is IdentityProviderServiceCredentials moduleCreds)
                    {
                        creds = moduleCreds;
                    }
                    else
                    {
                        throw new InvalidIdentityException($"No valid credentials found for {moduleIdentity.DeviceId}/{moduleIdentity.ModuleId}");
                    }

                    moduleWithIdentity = new KubernetesModule(
                        m.Value,
                        new KubernetesModuleIdentity(
                            moduleIdentity.IotHubHostname,
                            moduleIdentity.GatewayHostname,
                            moduleIdentity.DeviceId,
                            moduleIdentity.ModuleId,
                            creds));

                    k8sModules.Add(moduleWithIdentity);
                }
                else
                {
                    Events.UnableToProcessModule(m.Value);
                }

            }
            //string iotHubHostname, string deviceId, IKubernetes client, IModuleWithIdentity[] modules, Option<IRuntimeInfo> runtimeInfo, ICombinedConfigProvider<T> combinedConfigProvider
            var crdCommand = new KubernetesCrdCommand<CombinedDockerConfig>(this.iotHubHostname, this.deviceId, this.client, k8sModules.ToArray(), Option.Some(runtimeInfo), combinedConfigProvider as ICombinedConfigProvider<CombinedDockerConfig>);
            var planCommand = await this.commandFactory.WrapAsync(crdCommand);
            var plan = new List<ICommand>();
            plan.Add(planCommand);
            Events.PlanCreated(plan);
            return new Plan(plan);
        }


        public async Task<Plan> CreateShutdownPlanAsync(ModuleSet current)
        {
            var modulesWithIdentities = new List<KubernetesModule>();
            var crdCommand = new KubernetesCrdCommand<CombinedDockerConfig>(this.iotHubHostname, this.deviceId, this.client, modulesWithIdentities.ToArray(), Option.None<IRuntimeInfo>(), combinedConfigProvider as ICombinedConfigProvider<CombinedDockerConfig>);
            var planCommand = await this.commandFactory.WrapAsync(crdCommand);
            var plan = new List<ICommand>();
            plan.Add(planCommand);
            Events.PlanCreated(plan);
            return new Plan(plan);
        }
        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesPlanner<T>>();
            const int IdStart = AgentEventIds.KubernetesPlanner;

            enum EventIds
            {
                PlanCreated = IdStart,
                DesiredModules,
                CurrentModules,
                Identities,
                UnableToProcessModule
            }

            internal static void PlanCreated(IList<ICommand> commands)
            {
                Log.LogDebug((int)EventIds.PlanCreated, $"HealthRestartPlanner created Plan, with {commands.Count} command(s).");
            }

            internal static void LogDesired(ModuleSet desired)
            {
                IDictionary<string, IModule> modules = desired.Modules.ToImmutableDictionary();
                Log.LogDebug((int)EventIds.DesiredModules, $"List of desired modules is - {JsonConvert.SerializeObject(modules)}");
            }

            internal static void LogCurrent(ModuleSet current)
            {
                IDictionary<string, IModule> modules = current.Modules.ToImmutableDictionary();
                Log.LogDebug((int)EventIds.CurrentModules, $"List of current modules is - {JsonConvert.SerializeObject(modules)}");
            }

            internal static void LogIdentities(IEnumerable<IModuleIdentity> identities)
            {
                Log.LogDebug((int)EventIds.Identities, $"Current identities - {string.Join(", ", identities.Select(i => i.ModuleId))}");
            }

            internal static void UnableToProcessModule(IModule module)
            {
                Log.LogInformation((int)EventIds.UnableToProcessModule, $"Unable to process module {module.Name} add or update as the module identity could not be obtained");
            }
        }
    }
}
