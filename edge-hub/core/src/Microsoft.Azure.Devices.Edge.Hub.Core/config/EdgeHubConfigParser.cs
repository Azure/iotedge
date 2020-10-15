// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Creates EdgeHubConfig out of EdgeHubDesiredProperties.
    /// </summary>
    public class EdgeHubConfigParser
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeHubConfigParser>();

        public static EdgeHubConfig GetEdgeHubConfig(EdgeHubDesiredProperties desiredProperties, RouteFactory routeFactory)
        {
            Preconditions.CheckNotNull(desiredProperties, nameof(desiredProperties));

            ReadOnlyDictionary<string, RouteConfig> routes = ParseRoutes(desiredProperties, routeFactory);

            Option<BrokerConfig> brokerConfig = ParseBrokerConfig(desiredProperties.BrokerConfiguration);

            return new EdgeHubConfig(
                desiredProperties.SchemaVersion,
                routes,
                desiredProperties.StoreAndForwardConfiguration,
                brokerConfig);
        }

        static ReadOnlyDictionary<string, RouteConfig> ParseRoutes(EdgeHubDesiredProperties desiredProperties, RouteFactory routeFactory)
        {
            var routes = new Dictionary<string, RouteConfig>();
            if (desiredProperties.Routes != null)
            {
                foreach (KeyValuePair<string, RouteConfiguration> inputRoute in desiredProperties.Routes)
                {
                    try
                    {
                        Route route = routeFactory.Create(inputRoute.Value.Route, inputRoute.Value.Priority, inputRoute.Value.TimeToLiveSecs);
                        routes.Add(inputRoute.Key, new RouteConfig(inputRoute.Key, inputRoute.Value.Route, route));
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error parsing route {inputRoute.Key} - {ex.Message}", ex);
                    }
                }
            }

            return new ReadOnlyDictionary<string, RouteConfig>(routes);
        }

        static Option<BrokerConfig> ParseBrokerConfig(BrokerProperties properties)
        {
            if (properties != null)
            {
                return Option.Some(
                    new BrokerConfig(
                        ParseBridgeConfig(properties),
                        ParseAuthorizationConfig(properties)));
            }

            return Option.None<BrokerConfig>();
        }

        /// <summary>
        /// Important!: Validation logic should be in sync with mqtt_policy::MqttValidator in the Broker.
        ///
        /// EH Twin and policy definition in the Broker have different json schemas.
        /// This method converts twin schema (BrokerProperties) into broker policy schema (AuthorizationConfig),
        /// and validates it.
        /// </summary>
        static Option<AuthorizationConfig> ParseAuthorizationConfig(BrokerProperties properties)
        {
            ValidateAuthorizationConfig(properties.Authorizations);

            var order = 1;
            var result = new List<Statement>(properties.Authorizations?.Count ?? 0);
            foreach (var statement in properties.Authorizations)
            {
                // parse deny rules first, since we agreed that they take precedence
                // in case of conflicting rules.
                foreach (var rule in statement.Deny)
                {
                    result.Add(new Statement(
                        Effect.Deny,
                        statement.Identities,
                        rule.Operations,
                        rule.Resources));
                }

                foreach (var rule in statement.Allow)
                {
                    result.Add(new Statement(
                        Effect.Allow,
                        statement.Identities,
                        rule.Operations,
                        rule.Resources));
                }

                order++;
            }

            return Option.Some(new AuthorizationConfig(result));
        }

        static void ValidateAuthorizationConfig(AuthorizationProperties statements)
        {
            var order = 0;
            var errors = new List<string>();
            foreach (var statement in statements)
            {
                if (statement.Identities.Count == 0)
                {
                    errors.Add($"Statement {order}: Identities list must not be empty.");
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
                    ValidateRule(rule, order, errors);
                }

                foreach (var rule in statement.Deny)
                {
                    ValidateRule(rule, order, errors);
                }
            }
        }

        private static void ValidateRule(AuthorizationProperties.Rule rule, int order, List<string> errors)
        {
            if (rule.Operations.Count == 0)
            {
                errors.Add($"Statement {order}: Allow: Operations list must not be empty.");
            }

            if (rule.Resources.Count == 0)
            {
                errors.Add($"Statement {order}: Allow: Resources list must not be empty.");
            }

            foreach (var operation in rule.Operations)
            {
                if (!validOperations.Contains(operation))
                {
                    errors.Add($"Statement {order}: Unknown mqtt operation: {operation}. List of supported operations: mqtt:publish, mqtt:subscribe, mqtt:connect");
                }

                ValidateVariables(operation, order, errors);
            }

            foreach (var resource in rule.Resources)
            {
                if (string.IsNullOrEmpty(resource)
                    || !IsValidTopicFilter(resource))
                {
                    errors.Add($"Statement {order}: Resource (topic filter) is invalid: {resource}");
                }

                ValidateVariables(resource, order, errors);
            }
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

        static Option<BridgeConfig> ParseBridgeConfig(BrokerProperties properties)
        {
            return Option.None<BridgeConfig>();
        }

        static bool IsValidTopicFilter(string topic)
        {
            return true;
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
    }
}
