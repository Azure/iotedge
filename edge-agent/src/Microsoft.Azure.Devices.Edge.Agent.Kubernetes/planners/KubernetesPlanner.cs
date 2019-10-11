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
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class KubernetesPlanner : IPlanner
    {
        readonly IKubernetes client;
        readonly ICommandFactory commandFactory;
        readonly string deviceNamespace;
        readonly ResourceName resourceName;
        readonly ICombinedConfigProvider<CombinedKubernetesConfig> configProvider;

        public KubernetesPlanner(
            string deviceNamespace,
            ResourceName resourceName,
            IKubernetes client,
            ICommandFactory commandFactory,
            ICombinedConfigProvider<CombinedKubernetesConfig> configProvider)
        {
            this.resourceName = Preconditions.CheckNotNull(resourceName, nameof(resourceName));
            this.deviceNamespace = Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace));
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.commandFactory = Preconditions.CheckNotNull(commandFactory, nameof(commandFactory));
            this.configProvider = Preconditions.CheckNotNull(configProvider, nameof(configProvider));
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
            var groupedModules = desired.Modules.ToLookup(pair => KubeUtils.SanitizeK8sValue(pair.Key));
            if (groupedModules.Any(c => c.Count() > 1))
            {
                string nameList = groupedModules
                    .Where(c => c.Count() > 1)
                    .SelectMany(g => g, (pairs, pair) => pair.Key)
                    .Join(",");
                throw new InvalidIdentityException($"Deployment will cause a name collision in Kubernetes namespace, modules: [{nameList}]");
            }

            // TODO: improve this so it is generic for all potential module types.
            if (!desired.Modules.Values.All(p => p is IModule<DockerConfig>))
            {
                throw new InvalidModuleException($"Kubernetes deployment currently only handles type={typeof(DockerConfig).FullName}");
            }

            // This is a workaround for K8s Public Preview Refresh
            // TODO: remove this workaround when merging to the main release
            desired = new ModuleSet(desired.Modules.Remove(Constants.EdgeAgentModuleName));
            current = new ModuleSet(current.Modules.Remove(Constants.EdgeAgentModuleName));

            Diff moduleDifference = desired.Diff(current);

            Plan plan;
            if (!moduleDifference.IsEmpty)
            {
                // The "Plan" here is very simple - if we have any change, publish all desired modules to a EdgeDeployment CRD.
                // The CRD allows us to give the customer a Kubernetes-centric way to see the deployment
                // and the status of that deployment through the "edgedeployments" API.
                var crdCommand = new EdgeDeploymentCommand(this.deviceNamespace, this.resourceName, this.client, desired.Modules.Values, runtimeInfo, this.configProvider);
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
            static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesPlanner>();

            enum EventIds
            {
                PlanCreated = IdStart,
                DesiredModules,
                CurrentModules,
                Identities,
            }

            public static void PlanCreated(IReadOnlyList<ICommand> commands)
            {
                Log.LogDebug((int)EventIds.PlanCreated, $"KubernetesPlanner created Plan, with {commands.Count} command(s).");
            }

            public static void LogDesired(ModuleSet desired)
            {
                Log.LogDebug((int)EventIds.DesiredModules, $"List of desired modules is - {string.Join(", ", desired.Modules.Keys)}");
            }

            public static void LogCurrent(ModuleSet current)
            {
                Log.LogDebug((int)EventIds.CurrentModules, $"List of current modules is - {string.Join(", ", current.Modules.Keys)}");
            }

            public static void LogIdentities(IImmutableDictionary<string, IModuleIdentity> moduleIdentities)
            {
                Log.LogDebug((int)EventIds.Identities, $"List of module identities is - {string.Join(", ", moduleIdentities.Keys)}");
            }
        }
    }
}
