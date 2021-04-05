// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Validates broker config section in the EdgeHub twin.
    /// </summary>
    [Pure]
    public class BrokerPropertiesValidator
    {
        /// <summary>
        /// Important!: Validation logic should be in sync with mqtt_policy::MqttValidator in the Broker.
        ///
        /// Validates authorization policies and returns a list of errors (if any).
        /// </summary>
        public virtual IList<string> ValidateAuthorizationConfig(AuthorizationProperties properties)
        {
            Preconditions.CheckNotNull(properties, nameof(properties));

            var order = 0;
            var errors = new List<string>();
            foreach (var statement in properties)
            {
                if (statement.Identities.Count == 0)
                {
                    errors.Add($"Statement {order}: Identities list must not be empty");
                }

                foreach (var identity in statement.Identities)
                {
                    if (string.IsNullOrEmpty(identity))
                    {
                        errors.Add($"Statement {order}: Identity name is invalid: {identity}");
                    }

                    ValidateVariables(identity, order, errors);
                }

                foreach (var rule in statement.Allow)
                {
                    ValidateRule(rule, order, errors, "Allow");
                }

                foreach (var rule in statement.Deny)
                {
                    ValidateRule(rule, order, errors, "Deny");
                }

                order++;
            }

            return errors;
        }

        /// <summary>
        /// Important!: Validation logic should be in sync with validation logic in the Broker.
        ///
        /// Validates bridge config and returns a list of errors (if any).
        /// </summary>
        public virtual IList<string> ValidateBridgeConfig(BridgeConfig properties)
        {
            Preconditions.CheckNotNull(properties, nameof(properties));

            var errors = new List<string>();
            foreach (var bridge in properties)
            {
                ValidateBridge(bridge, errors);
            }

            return errors;
        }

        static void ValidateBridge(Bridge bridge, List<string> errors)
        {
            if (string.IsNullOrEmpty(bridge.Endpoint))
            {
                errors.Add($"Bridge endpoint must not be empty");
            }

            if (!bridge.Endpoint.Equals("$upstream", StringComparison.InvariantCultureIgnoreCase)
                && bridge.Settings.Count == 0)
            {
                errors.Add($"Bridge {bridge.Endpoint}: Settings must not be empty");
            }

            var order = 0;
            foreach (var setting in bridge.Settings)
            {
                if (setting.Topic != null
                    && !IsValidTopicFilter(setting.Topic))
                {
                    errors.Add($"Bridge {bridge.Endpoint}: Rule {order}: Topic is invalid: {setting.Topic}");
                }

                if (setting.InPrefix.Contains("+")
                    || setting.InPrefix.Contains("#"))
                {
                    errors.Add($"Bridge {bridge.Endpoint}: Rule {order}: InPrefix must not contain wildcards (+, #)");
                }

                if (setting.OutPrefix.Contains("+")
                    || setting.OutPrefix.Contains("#"))
                {
                    errors.Add($"Bridge {bridge.Endpoint}: Rule {order}: OutPrefix must not contain wildcards (+, #)");
                }

                order++;
            }
        }

        static void ValidateRule(AuthorizationProperties.Rule rule, int order, List<string> errors, string source)
        {
            if (rule.Operations.Count == 0)
            {
                errors.Add($"Statement {order}: {source}: Operations list must not be empty");
            }

            if (rule.Resources.Count == 0 && !IsConnectOperation(rule))
            {
                errors.Add($"Statement {order}: {source}: Resources list must not be empty");
            }

            foreach (var operation in rule.Operations)
            {
                if (!validOperations.Contains(operation))
                {
                    errors.Add($"Statement {order}: {source}: Unknown mqtt operation: {operation}. List of supported operations: mqtt:publish, mqtt:subscribe, mqtt:connect");
                }

                ValidateVariables(operation, order, errors);
            }

            foreach (var resource in rule.Resources)
            {
                if (string.IsNullOrEmpty(resource)
                    || !IsValidTopicFilter(resource))
                {
                    errors.Add($"Statement {order}: {source}: Resource (topic filter) is invalid: {resource}");
                }

                ValidateVariables(resource, order, errors);
            }
        }

        private static bool IsConnectOperation(AuthorizationProperties.Rule rule)
        {
            return rule.Operations.Count == 1 && rule.Operations[0] == "mqtt:connect";
        }

        static void ValidateVariables(string value, int order, List<string> errors)
        {
            foreach (var variable in ExtractVariable(value))
            {
                if (!validVariables.Contains(variable))
                {
                    errors.Add($"Statement {order}: Invalid variable name: {variable}");
                }
            }
        }

        static bool IsValidTopicFilter(string topic)
        {
            return !invalidTopicFilterRegex.IsMatch(topic);
        }

        static IEnumerable<string> ExtractVariable(string value)
        {
            foreach (Match match in varRegex.Matches(value))
            {
                yield return match.Value;
            }
        }

        static readonly string[] validOperations = new[] { "mqtt:publish", "mqtt:subscribe", "mqtt:connect" };

        static readonly string[] validVariables = new[]
        {
            "{{iot:identity}}",
            "{{iot:device_id}}",
            "{{iot:module_id}}",
            "{{mqtt:client_id}}",
            "{{iot:this_device_id}}",
        };

        static readonly Regex varRegex = new Regex("{{[^}]*}}");

        // This regex matches if any of the following is violated:
        //  - The multi-level wildcard character MUST be specified either
        //    on its own or following a topic level separator. In either case
        //    it MUST be the last character specified in the Topic Filter [MQTT-4.7.1-2].
        //
        // - The single-level wildcard can be used at any level in the Topic Filter,
        //   including first and last levels. Where it is used it MUST occupy an entire
        //   level of the filter [MQTT-4.7.1-3].
        //
        // In other words, the regex matches if a topic filter:
        // - has + that is surrounded by any char other than '/'.
        // - has # not at the end.
        // - has # lead by any char other than '/'.
        static readonly Regex invalidTopicFilterRegex = new Regex(@"[^\/]\+[^\/]?|\+[^\/]|[^\/]#|#.+$");
    }
}
