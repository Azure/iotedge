// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Creates EdgeHubConfig out of EdgeHubDesiredProperties. Also validates the
    /// desired properties. Throws an exception if validation failed.
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

        public EdgeHubConfig GetEdgeHubConfig(string twinJson)
        {
            EdgeHubConfig edgeHubConfig;

            var twinJObject = JObject.Parse(twinJson);
            if (twinJObject.TryGetValue(Core.Constants.SchemaVersionKey, out JToken token))
            {
                Version version;

                try
                {
                    version = new Version(token.ToString());
                }
                catch (Exception e)
                {
                    throw new InvalidSchemaVersionException($"Error parsing schema version string: {token}, Exception: {e.Message}");
                }

                // Parse the JSON for 1.x
                if (version.Major == Core.Constants.SchemaVersion_1_0.Major)
                {
                    if (version.Minor == Core.Constants.SchemaVersion_1_0.Minor)
                    {
                        var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_0>(twinJson);
                        edgeHubConfig = this.GetEdgeHubConfig(desiredProperties);
                    }
                    else if (version.Minor == Core.Constants.SchemaVersion_1_1.Minor)
                    {
                        var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_1>(twinJson);
                        edgeHubConfig = this.GetEdgeHubConfig(desiredProperties);
                    }
                    else if (version.Minor == Core.Constants.SchemaVersion_1_2.Minor)
                    {
                        var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_2>(twinJson);
                        edgeHubConfig = this.GetEdgeHubConfig(desiredProperties);
                    }
                    else if (version.Minor == Core.Constants.SchemaVersion_1_3.Minor)
                    {
                        var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_3>(twinJson);
                        edgeHubConfig = this.GetEdgeHubConfig(desiredProperties);
                    }
                    else
                    {
                        throw new InvalidSchemaVersionException($"EdgeHub config contains unsupported SchemaVersion: {version}");
                    }
                }
                else
                {
                    throw new InvalidSchemaVersionException($"EdgeHub config contains unsupported SchemaVersion: {version}");
                }
            }
            else
            {
                throw new InvalidSchemaVersionException("EdgeHub config missing SchemaVersion");
            }

            return edgeHubConfig;
        }

        public EdgeHubConfig GetEdgeHubConfig(EdgeHubDesiredProperties_1_0 desiredProperties)
        {
            Preconditions.CheckNotNull(desiredProperties, nameof(desiredProperties));

            var routes = new Dictionary<string, RouteConfig>();
            if (desiredProperties.Routes != null)
            {
                foreach (KeyValuePair<string, string> inputRoute in desiredProperties.Routes)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(inputRoute.Value))
                        {
                            Route route = this.routeFactory.Create(inputRoute.Value);
                            routes.Add(inputRoute.Key, new RouteConfig(inputRoute.Key, inputRoute.Value, route));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error parsing route {inputRoute.Key} - {ex.Message}", ex);
                    }
                }
            }

            return new EdgeHubConfig(
                desiredProperties.SchemaVersion,
                new ReadOnlyDictionary<string, RouteConfig>(routes),
                desiredProperties.StoreAndForwardConfiguration,
                Option.None<BrokerConfig>(),
                Option.None<ManifestIntegrity>());
        }

        public EdgeHubConfig GetEdgeHubConfig(EdgeHubDesiredProperties_1_1 desiredProperties)
        {
            Preconditions.CheckNotNull(desiredProperties, nameof(desiredProperties));
            ReadOnlyDictionary<string, RouteConfig> routes = ParseRoutesWithPriority(desiredProperties.Routes, this.routeFactory);

            return new EdgeHubConfig(
                desiredProperties.SchemaVersion,
                routes,
                desiredProperties.StoreAndForwardConfiguration,
                Option.None<BrokerConfig>(),
                Option.None<ManifestIntegrity>());
        }

        public EdgeHubConfig GetEdgeHubConfig(EdgeHubDesiredProperties_1_2 desiredProperties)
        {
            Preconditions.CheckNotNull(desiredProperties, nameof(desiredProperties));
            ReadOnlyDictionary<string, RouteConfig> routes = ParseRoutesWithPriority(desiredProperties.Routes, this.routeFactory);
            Option<BrokerConfig> brokerConfig = this.ParseBrokerConfig(desiredProperties.BrokerConfiguration);

            return new EdgeHubConfig(
                desiredProperties.SchemaVersion,
                routes,
                desiredProperties.StoreAndForwardConfiguration,
                brokerConfig,
                Option.None<ManifestIntegrity>());
        }

        public EdgeHubConfig GetEdgeHubConfig(EdgeHubDesiredProperties_1_3 desiredProperties)
        {
            Preconditions.CheckNotNull(desiredProperties, nameof(desiredProperties));
            ReadOnlyDictionary<string, RouteConfig> routes = ParseRoutesWithPriority(desiredProperties.Routes, this.routeFactory);
            Option<BrokerConfig> brokerConfig = this.ParseBrokerConfig(desiredProperties.BrokerConfiguration);

            return new EdgeHubConfig(
                desiredProperties.SchemaVersion,
                routes,
                desiredProperties.StoreAndForwardConfiguration,
                brokerConfig,
                desiredProperties.Integrity);
        }

        static ReadOnlyDictionary<string, RouteConfig> ParseRoutesWithPriority(IDictionary<string, RouteSpec> routeSpecs, RouteFactory routeFactory)
        {
            var routes = new Dictionary<string, RouteConfig>();
            foreach (KeyValuePair<string, RouteSpec> inputRoute in routeSpecs)
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
            if (properties.Authorizations.Count == 0)
            {
                return Option.None<AuthorizationConfig>();
            }

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
            if (properties.Bridges.Count == 0)
            {
                return Option.None<BridgeConfig>();
            }

            IList<string> errors = this.validator.ValidateBridgeConfig(properties.Bridges);
            if (errors.Count > 0)
            {
                string message = string.Join("; ", errors);
                throw new InvalidOperationException($"Error validating bridge configuration: {message}");
            }

            return Option.Some(properties.Bridges);
        }
    }
}
