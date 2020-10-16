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

            EdgeHubDesiredProperties properties = ConfigTestData.GetTestData();

            var authzProperties = properties.BrokerConfiguration.Authorizations;

            IList<string> errors = validator.ValidateAuthorizationConfig(properties.BrokerConfiguration.Authorizations);

            Assert.Equal(0, errors.Count);
        }

        [Fact]
        public void ValidateAuthorizationConfig_EmptyElements()
        {
            var validator = new BrokerPropertiesValidator();

            EdgeHubDesiredProperties properties = ConfigTestData.GetTestData();

            var authzProperties = properties.BrokerConfiguration.Authorizations;

            // arrange some errors
            authzProperties[0].Identities[0] = string.Empty;
            authzProperties[1].Allow[0].Operations.RemoveAt(0);
            authzProperties[1].Allow[0].Operations.RemoveAt(0);

            IList<string> errors = validator.ValidateAuthorizationConfig(properties.BrokerConfiguration.Authorizations);

            Assert.Equal(2, errors.Count);
            Assert.Equal("Statement 0: Identity name is invalid: ", errors[0]);
            Assert.Equal("Statement 1: Allow: Operations list must not be empty", errors[1]);
        }

        [Fact]
        public void ValidateAuthorizationConfig_InvalidOperation()
        {
            var validator = new BrokerPropertiesValidator();

            EdgeHubDesiredProperties properties = ConfigTestData.GetTestData();

            var authzProperties = properties.BrokerConfiguration.Authorizations;

            // arrange some errors
            authzProperties[0].Deny[0].Operations[0] = "invalid";

            IList<string> errors = validator.ValidateAuthorizationConfig(properties.BrokerConfiguration.Authorizations);

            Assert.Equal(1, errors.Count);
            Assert.Equal(
                "Statement 0: Unknown mqtt operation: invalid. "
                + "List of supported operations: mqtt:publish, mqtt:subscribe, mqtt:connect",
                errors[0]);
        }

        [Fact]
        public void ValidateAuthorizationConfig_InvalidTopicFilters()
        {
            var validator = new BrokerPropertiesValidator();

            EdgeHubDesiredProperties properties = ConfigTestData.GetTestData();

            var authzProperties = properties.BrokerConfiguration.Authorizations;

            // arrange some errors
            authzProperties[0].Deny[0].Resources[0] = "topic/#/";
            authzProperties[1].Allow[0].Resources[0] = "topic+";

            IList<string> errors = validator.ValidateAuthorizationConfig(properties.BrokerConfiguration.Authorizations);

            Assert.Equal(2, errors.Count);
            Assert.Equal("Statement 0: Resource (topic filter) is invalid: topic/#/", errors[0]);
            Assert.Equal("Statement 1: Resource (topic filter) is invalid: topic+", errors[1]);
        }
    }
}
