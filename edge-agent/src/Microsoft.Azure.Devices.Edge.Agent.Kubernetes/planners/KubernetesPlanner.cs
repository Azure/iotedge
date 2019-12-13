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
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class KubernetesPlanner : IPlanner
    {
        readonly IKubernetes client;
        readonly ICommandFactory commandFactory;
        readonly string deviceNamespace;
        readonly ResourceName resourceName;
        readonly ICombinedConfigProvider<CombinedKubernetesConfig> configProvider;
        readonly JsonSerializerSettings serializerSettings;

        readonly KubernetesModuleOwner moduleOwner;

        public KubernetesPlanner(
            string deviceNamespace,
            ResourceName resourceName,
            IKubernetes client,
            ICommandFactory commandFactory,
            ICombinedConfigProvider<CombinedKubernetesConfig> configProvider,
            KubernetesModuleOwner moduleOwner)
        {
            this.resourceName = Preconditions.CheckNotNull(resourceName, nameof(resourceName));
            this.deviceNamespace = Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace));
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.commandFactory = Preconditions.CheckNotNull(commandFactory, nameof(commandFactory));
            this.configProvider = Preconditions.CheckNotNull(configProvider, nameof(configProvider));
            this.moduleOwner = Preconditions.CheckNotNull(moduleOwner);
            this.serializerSettings = EdgeDeploymentSerialization.SerializerSettings;
        }

        public async Task<Plan> PlanAsync(
            ModuleSet desired,
            ModuleSet current,
            IRuntimeInfo runtimeInfo,
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities)
        {
            Events.LogDesired(desired);

            // We receive current ModuleSet from Agent based on what it reports (i.e. pods).
            // We need to rebuild the current ModuleSet based on deployments (i.e. CRD).
            Option<EdgeDeploymentDefinition> activeDeployment = await this.GetCurrentEdgeDeploymentDefinitionAsync();
            ModuleSet currentModules =
                activeDeployment.Match(
                a => ModuleSet.Create(a.Spec.ToArray()),
                () => ModuleSet.Empty);

            Events.LogCurrent(currentModules);

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

            Diff moduleDifference = desired.Diff(currentModules);

            Plan plan;
            if (!moduleDifference.IsEmpty)
            {
                // The "Plan" here is very simple - if we have any change, publish all desired modules to a EdgeDeployment CRD.
                var crdCommand = new EdgeDeploymentCommand(this.deviceNamespace, this.resourceName, this.client, desired.Modules.Values, activeDeployment, runtimeInfo, this.configProvider, this.moduleOwner);
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

        async Task<Option<EdgeDeploymentDefinition>> GetCurrentEdgeDeploymentDefinitionAsync()
        {
            Option<EdgeDeploymentDefinition> activeDeployment;
            try
            {
                JObject currentDeployment = await this.client.GetNamespacedCustomObjectAsync(
                    KubernetesConstants.EdgeDeployment.Group,
                    KubernetesConstants.EdgeDeployment.Version,
                    this.deviceNamespace,
                    KubernetesConstants.EdgeDeployment.Plural,
                    this.resourceName) as JObject;

                activeDeployment = Option.Maybe(currentDeployment)
                    .Map(deployment => deployment.ToObject<EdgeDeploymentDefinition>(JsonSerializer.Create(this.serializerSettings)));
            }
            catch (Exception parseException)
            {
                Events.UnableToListDeployments(this.deviceNamespace, parseException);
                activeDeployment = Option.None<EdgeDeploymentDefinition>();
            }

            return activeDeployment;
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
                ListModules,
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

            public static void UnableToListDeployments(string deviceNamespace, Exception exception)
            {
                Log.LogDebug((int)EventIds.ListModules, exception, $"Unable to list deployments in namespace {deviceNamespace}");
            }
        }
    }
}
