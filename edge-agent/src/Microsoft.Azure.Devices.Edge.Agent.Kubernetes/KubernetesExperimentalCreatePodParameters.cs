// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    public class KubernetesExperimentalCreatePodParameters
    {
        public Option<IDictionary<string, string>> NodeSelector { get; }

        public Option<V1ResourceRequirements> Resources { get; }

        public Option<IReadOnlyList<KubernetesModuleVolumeSpec>> Volumes { get; }

        public Option<V1PodSecurityContext> SecurityContext { get; }

        KubernetesExperimentalCreatePodParameters(
            Option<IDictionary<string, string>> nodeSelector,
            Option<V1ResourceRequirements> resources,
            Option<IReadOnlyList<KubernetesModuleVolumeSpec>> volumes,
            Option<V1PodSecurityContext> securityContext)
        {
            this.NodeSelector = nodeSelector;
            this.Resources = resources;
            this.Volumes = volumes;
            this.SecurityContext = securityContext;
        }

        static class ExperimentalParameterNames
        {
            public const string Section = "k8s-experimental";
            public const string NodeSelector = "NodeSelector";
            public const string Resources = "Resources";
            public const string Volumes = "Volumes";
            public const string SecurityContext = "SecurityContext";
        }

        public static Option<KubernetesExperimentalCreatePodParameters> Parse(IDictionary<string, JToken> other)
            => Option.Maybe(other).FlatMap(options => options.Get(ExperimentalParameterNames.Section).FlatMap(ParseParameters));

        static Option<KubernetesExperimentalCreatePodParameters> ParseParameters(JToken experimental)
            => Option.Maybe(experimental as JObject)
                .Map(ParseParameters)
                .Else(
                    () =>
                    {
                        Events.UnableToParseExperimentalOptions(experimental);
                        return Option.None<KubernetesExperimentalCreatePodParameters>();
                    });

        static KubernetesExperimentalCreatePodParameters ParseParameters(JObject experimental)
        {
            Dictionary<string, JToken> options = PrepareSupportedOptionsStore(experimental);

            Option<IDictionary<string, string>> nodeSelector = options.Get(ExperimentalParameterNames.NodeSelector)
                .FlatMap(option => Option.Maybe(option.ToObject<IDictionary<string, string>>()));

            var resources = options.Get(ExperimentalParameterNames.Resources)
                .FlatMap(option => Option.Maybe(option.ToObject<V1ResourceRequirements>()));

            var volumes = options.Get(ExperimentalParameterNames.Volumes)
                .FlatMap(option => Option.Maybe(option.ToObject<IReadOnlyList<KubernetesModuleVolumeSpec>>()));

            var securityContext = options.Get(ExperimentalParameterNames.SecurityContext)
                .FlatMap(option => Option.Maybe(option.ToObject<V1PodSecurityContext>()));

            return new KubernetesExperimentalCreatePodParameters(nodeSelector, resources, volumes, securityContext);
        }

        static Dictionary<string, JToken> PrepareSupportedOptionsStore(JObject experimental)
        {
            var options = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in experimental.Properties())
            {
                if (!KnownExperimentalOptions.Contains(property.Name))
                {
                    Events.UnknownExperimentalOption(property.Name);
                    continue;
                }

                options[property.Name] = property.Value;
            }

            return options;
        }

        static readonly HashSet<string> KnownExperimentalOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ExperimentalParameterNames.NodeSelector,
            ExperimentalParameterNames.Resources,
            ExperimentalParameterNames.Volumes,
            ExperimentalParameterNames.SecurityContext
        };

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesExperimentalCreateOptions;
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeDeploymentOperator>();

            enum EventIds
            {
                UnknownExperimentalOption = IdStart
            }

            public static void UnknownExperimentalOption(string name)
            {
                Log.LogWarning((int)EventIds.UnknownExperimentalOption, $"Unknown Kubernetes CreateOption {name}.");
            }

            public static void UnableToParseExperimentalOptions(JToken experimental)
            {
                Log.LogWarning((int)EventIds.UnknownExperimentalOption, $"Unable to parse Kubernetes CreateOptions. Expected JObject but: {experimental.Type} found: {experimental}");
            }
        }
    }
}
