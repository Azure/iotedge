// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Config
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class BrokerPropertiesValidatorTest
    {
        [Fact]
        public void ValidateAuthorizationConfig_ValidInput()
        {
            var validator = new BrokerPropertiesValidator();

            EdgeHubDesiredProperties_1_2 properties = ConfigTestData.GetTestData();

            var authzProperties = properties.BrokerConfiguration.Authorizations;

            IList<string> errors = validator.ValidateAuthorizationConfig(authzProperties);

            Assert.Equal(0, errors.Count);
        }

        [Fact]
        public void ValidateAuthorizationConfig_EmptyElements()
        {
            var validator = new BrokerPropertiesValidator();

            EdgeHubDesiredProperties_1_2 properties = ConfigTestData.GetTestData();

            var authzProperties = properties.BrokerConfiguration.Authorizations;

            // arrange some errors
            authzProperties[0].Identities[0] = string.Empty;
            authzProperties[0].Deny[0].Resources.Clear();
            authzProperties[1].Identities.Clear();
            authzProperties[1].Allow[0].Operations.Clear();

            IList<string> errors = validator.ValidateAuthorizationConfig(authzProperties);

            Assert.Equal(4, errors.Count);
            Assert.Equal("Statement 0: Identity name is invalid: ", errors[0]);
            Assert.Equal("Statement 0: Deny: Resources list must not be empty", errors[1]);
            Assert.Equal("Statement 1: Identities list must not be empty", errors[2]);
            Assert.Equal("Statement 1: Allow: Operations list must not be empty", errors[3]);
        }

        [Fact]
        public void ValidateAuthorizationConfig_InvalidOperation()
        {
            var validator = new BrokerPropertiesValidator();

            EdgeHubDesiredProperties_1_2 properties = ConfigTestData.GetTestData();

            var authzProperties = properties.BrokerConfiguration.Authorizations;

            // arrange some errors
            authzProperties[0].Deny[0].Operations[0] = "invalid";

            IList<string> errors = validator.ValidateAuthorizationConfig(authzProperties);

            Assert.Equal(1, errors.Count);
            Assert.Equal(
                "Statement 0: Deny: Unknown mqtt operation: invalid. "
                + "List of supported operations: mqtt:publish, mqtt:subscribe, mqtt:connect",
                errors[0]);
        }

        [Fact]
        public void ValidateAuthorizationConfig_InvalidTopicFilters()
        {
            var validator = new BrokerPropertiesValidator();

            EdgeHubDesiredProperties_1_2 properties = ConfigTestData.GetTestData();

            var authzProperties = properties.BrokerConfiguration.Authorizations;

            // arrange some errors
            authzProperties[0].Deny[0].Resources[0] = "topic/#/";
            authzProperties[1].Allow[0].Resources[0] = "topic+";

            IList<string> errors = validator.ValidateAuthorizationConfig(authzProperties);

            Assert.Equal(2, errors.Count);
            Assert.Equal("Statement 0: Deny: Resource (topic filter) is invalid: topic/#/", errors[0]);
            Assert.Equal("Statement 1: Allow: Resource (topic filter) is invalid: topic+", errors[1]);
        }

        [Fact]
        public void ValidateAuthorizationConfig_InvalidVariableNames()
        {
            var validator = new BrokerPropertiesValidator();

            EdgeHubDesiredProperties_1_2 properties = ConfigTestData.GetTestData();

            var authzProperties = properties.BrokerConfiguration.Authorizations;

            // arrange some errors
            authzProperties[0].Identities[0] = "{{anywhat}}";
            authzProperties[1].Allow[0].Resources[0] = "topic/{{invalid}}/{{myothervar}}";

            IList<string> errors = validator.ValidateAuthorizationConfig(authzProperties);

            Assert.Equal(3, errors.Count);
            Assert.Equal("Statement 0: Invalid variable name: {{anywhat}}", errors[0]);
            Assert.Equal("Statement 1: Invalid variable name: {{invalid}}", errors[1]);
            Assert.Equal("Statement 1: Invalid variable name: {{myothervar}}", errors[2]);
        }

        [Fact]
        public void ValidateAuthorizationConfig_EmptyResourceAllowedForConnectOperation()
        {
            var validator = new BrokerPropertiesValidator();

            EdgeHubDesiredProperties_1_2 properties = ConfigTestData.GetTestData();

            var authzProperties = properties.BrokerConfiguration.Authorizations;

            // arrange connect op with no resources.
            authzProperties[0].Deny[0].Operations.Clear();
            authzProperties[0].Deny[0].Operations.Insert(0, "mqtt:connect");
            authzProperties[0].Deny[0].Resources.Clear();

            IList<string> errors = validator.ValidateAuthorizationConfig(authzProperties);

            Assert.Equal(0, errors.Count);
        }

        [Fact]
        public void ValidateBridgeConfig_ValidInput()
        {
            var validator = new BrokerPropertiesValidator();

            EdgeHubDesiredProperties_1_2 properties = ConfigTestData.GetTestData();

            IList<string> errors = validator.ValidateBridgeConfig(properties.BrokerConfiguration.Bridges);

            Assert.Equal(0, errors.Count);
        }

        [Fact]
        public void ValidateBridgeConfig_EmptyElements()
        {
            var validator = new BrokerPropertiesValidator();

            var bridgeConfig = new BridgeConfig
            {
                new Bridge(string.Empty, new List<Settings>
                {
                    new Settings(Direction.In, string.Empty, string.Empty, string.Empty)
                }),
                new Bridge("floor2", new List<Settings> { })
            };

            IList<string> errors = validator.ValidateBridgeConfig(bridgeConfig);

            Assert.Equal(2, errors.Count);
            Assert.Equal("Bridge endpoint must not be empty", errors[0]);
            Assert.Equal("Bridge floor2: Settings must not be empty", errors[1]);
        }

        [Fact]
        public void ValidateBridgeConfig_InvalidTopicOrPrefix()
        {
            var validator = new BrokerPropertiesValidator();

            var bridgeConfig = new BridgeConfig
            {
                new Bridge("$upstream", new List<Settings>
                {
                    new Settings(Direction.In, "topic/#/a", "local/#", "remote/+/")
                })
            };

            IList<string> errors = validator.ValidateBridgeConfig(bridgeConfig);

            Assert.Equal(3, errors.Count);
            Assert.Equal("Bridge $upstream: Rule 0: Topic is invalid: topic/#/a", errors[0]);
            Assert.Equal("Bridge $upstream: Rule 0: InPrefix must not contain wildcards (+, #)", errors[1]);
            Assert.Equal("Bridge $upstream: Rule 0: OutPrefix must not contain wildcards (+, #)", errors[2]);
        }
    }
}
