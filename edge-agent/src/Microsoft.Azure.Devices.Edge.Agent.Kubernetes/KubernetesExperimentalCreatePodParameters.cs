// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    public class KubernetesExperimentalCreatePodParameters
    {
        public Option<IDictionary<string, string>> NodeSelector { get; }

        public KubernetesExperimentalCreatePodParameters(Option<IDictionary<string, string>> nodeSelector)
        {
            this.NodeSelector = nodeSelector;
        }

        static class ExperimentalParameterNames
        {
            public const string Section = "k8s-experimental";
            public const string NodeSelector = "NodeSelector";
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
                .FlatMap(selector => Option.Maybe(selector.ToObject<IDictionary<string, string>>()))
                .Filter(selector => selector.Any());

            return new KubernetesExperimentalCreatePodParameters(nodeSelector);
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

        static readonly HashSet<string> KnownExperimentalOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ExperimentalParameterNames.NodeSelector };

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
