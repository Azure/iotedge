// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Planners
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using k8s;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class KubernetesPlanner<T> : IPlanner
    {
        readonly IKubernetes client;
        readonly ICommandFactory commandFactory;
        readonly ICombinedConfigProvider<T> combinedConfigProvider;
        readonly string deviceNamespace;
        readonly string iotHubHostname;
        readonly string deviceId;

        public KubernetesPlanner(
            string deviceNamespace,
            string iotHubHostname,
            string deviceId,
            IKubernetes client,
            ICommandFactory commandFactory,
            ICombinedConfigProvider<T> combinedConfigProvider)
        {
            this.deviceNamespace = Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace));
            this.iotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
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
            Events.LogIdentities(moduleIdentities);

            // Check that module names sanitize and remain unique.
            var groupedModules = desired.Modules.GroupBy(pair => KubeUtils.SanitizeK8sValue(pair.Key)).ToArray();
            if (groupedModules.Any(c => c.Count() > 1))
            {
                string nameList = groupedModules.Where(c => c.Count() > 1).SelectMany(g => g, (pairs, pair) => pair.Key).Join(",");
                throw new InvalidIdentityException($"Deployment will cause a name collision in Kubernetes namespace, modules: [{nameList}]");
            }

            // TODO: improve this so it is generic for all potential module types.
            if (!desired.Modules.Values.All(p => p is IModule<DockerConfig>))
            {
                throw new InvalidModuleException($"Kubernetes deployment currently only handles type={typeof(T).FullName}");
            }

            Diff moduleDifference = desired.Diff(current);

            Plan plan;
            if (!moduleDifference.IsEmpty)
            {
                // The "Plan" here is very simple - if we have any change, publish all desired modules to a CRD.
                // The CRD allows us to give the customer a Kubernetes-centric way to see the deployment
                // and the status of that deployment through the "edgedeployments" API.
                var k8sModules = desired.Modules.Select(m => new KubernetesModule<DockerConfig>(m.Value as IModule<DockerConfig>));

                var crdCommand = new KubernetesCrdCommand<CombinedDockerConfig>(this.deviceNamespace, this.iotHubHostname, this.deviceId, this.client, k8sModules.ToArray(), Option.Some(runtimeInfo), this.combinedConfigProvider as ICombinedConfigProvider<CombinedDockerConfig>);
                var planCommand = await this.commandFactory.WrapAsync(crdCommand);
                var planList = new List<ICommand>
                {
                    planCommand
                };
                Events.PlanCreated(planList);
                plan = new Plan(planList);
            }
            else
            {
                plan = Plan.Empty;
            }

            return plan;
        }

        public Task<Plan> CreateShutdownPlanAsync(ModuleSet current) => Task.FromResult(Plan.Empty);

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesPlanner;
            static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesPlanner<T>>();

            enum EventIds
            {
                PlanCreated = IdStart,
                DesiredModules,
                CurrentModules,
                Identities,
            }

            internal static void PlanCreated(IList<ICommand> commands)
            {
                Log.LogDebug((int)EventIds.PlanCreated, $"KubernetesPlanner created Plan, with {commands.Count} command(s).");
            }

            internal static void LogDesired(ModuleSet desired)
            {
                Log.LogDebug((int)EventIds.DesiredModules, $"List of desired modules is - {string.Join(", ", desired.Modules.Keys)}");
            }

            internal static void LogCurrent(ModuleSet current)
            {
                Log.LogDebug((int)EventIds.CurrentModules, $"List of current modules is - {string.Join(", ", current.Modules.Keys)}");
            }

            internal static void LogIdentities(IImmutableDictionary<string, IModuleIdentity> moduleIdentities)
            {
                Log.LogDebug((int)EventIds.Identities, $"List of module identities is - {string.Join(", ", moduleIdentities.Keys)}");
            }
        }
    }
}
