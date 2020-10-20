// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
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

        readonly BrokerPropertiesValidator validator;

        public EdgeHubConfigParser(RouteFactory routeFactory, BrokerPropertiesValidator validator)
        {
            this.routeFactory = Preconditions.CheckNotNull(routeFactory, nameof(routeFactory));
            this.validator = Preconditions.CheckNotNull(validator, nameof(validator));
        }

        public EdgeHubConfig GetEdgeHubConfig(EdgeHubDesiredProperties desiredProperties)
        {
            Preconditions.CheckNotNull(desiredProperties, nameof(desiredProperties));

            ReadOnlyDictionary<string, RouteConfig> routes = ParseRoutes(desiredProperties, this.routeFactory);

            Option<BrokerConfig> brokerConfig = this.ParseBrokerConfig(desiredProperties.BrokerConfiguration);

            TwinIntegrity integrity = new TwinIntegrity(new TwinHeader(string.Empty, string.Empty, string.Empty), new TwinSignature(string.Empty, string.Empty));

            return new EdgeHubConfig(
                desiredProperties.SchemaVersion,
                routes,
                desiredProperties.StoreAndForwardConfiguration,
                brokerConfig,
                integrity);
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

        Option<BrokerConfig> ParseBrokerConfig(BrokerProperties properties)
        {
            if (properties != null)
            {
                return Option.Some(
                    new BrokerConfig(
                        this.ParseBridgeConfig(properties),
                        this.ParseAuthorizationConfig(properties)));
            }

            return Option.None<BrokerConfig>();
        }

        /// <summary>
        /// EH Twin and policy definition in the Broker have different json schemas.
        /// This method converts twin schema (BrokerProperties) into broker policy schema (AuthorizationConfig),
        /// and validates it.
        /// </summary>
        Option<AuthorizationConfig> ParseAuthorizationConfig(BrokerProperties properties)
        {
            IList<string> errors = this.validator.ValidateAuthorizationConfig(properties.Authorizations);
            if (errors.Count > 0)
            {
                string message = string.Join("; ", errors);
                throw new InvalidOperationException($"Error validating authorization policy: {message}");
            }

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
            }

            return Option.Some(new AuthorizationConfig(result));
        }

        Option<BridgeConfig> ParseBridgeConfig(BrokerProperties properties)
        {
            return Option.None<BridgeConfig>();
        }
    }
}
