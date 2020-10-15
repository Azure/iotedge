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

        readonly RouteFactory routeFactory;

        public EdgeHubConfigParser(RouteFactory routeFactory)
        {
            this.routeFactory = routeFactory;
        }

        public EdgeHubConfig GetEdgeHubConfig(EdgeHubDesiredProperties desiredProperties)
        {
            Preconditions.CheckNotNull(desiredProperties, nameof(desiredProperties));

            ReadOnlyDictionary<string, RouteConfig> routes = ParseRoutes(desiredProperties, this.routeFactory);

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
            BrokerPropertiesValidator.ValidateAuthorizationConfig(properties.Authorizations);

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

        static Option<BridgeConfig> ParseBridgeConfig(BrokerProperties properties)
        {
            return Option.None<BridgeConfig>();
        }
    }
}
