// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    internal class MockEnvironment : IEnvironmentWrapper
    {
        public Dictionary<string, string> Map = new Dictionary<string, string>();

        public Option<string> GetVariable(string variableName)
        {
            if (this.Map.ContainsKey(variableName))
            {
                return Option.Some(this.Map[variableName]);
            }
            else
            {
                return Option.None<string>();
            }
        }

        public void SetVariable(string variableName, string value)
        {
            // Check for entry not existing and add to dictionary
            this.Map[variableName] = value;
        }
    }

    [Unit]
    public class NestedEdgeParentUriParserTest
    {
        [Theory]
        [InlineData("$upstream:9000/comp/ea:tag1", "parentaddress:9000/comp/ea:tag1", null, "IOTEDGE_GATEWAYHOSTNAME", "parentaddress")]
        [InlineData("$upstream:9000/ea:tag1", "parentaddress:9000/ea:tag1", null, "IOTEDGE_GATEWAYHOSTNAME", "parentaddress")]
        [InlineData("http://$upstream:443/dummy/test", "http://parentaddress:443/dummy/test", null, "IOTEDGE_GATEWAYHOSTNAME", "parentaddress")]
        [InlineData("$upstream:9000/comp/ea:tag1", null, typeof(InvalidOperationException), "dummyValue", "parentaddress")]
        [InlineData("$dummy:9000/comp/ea:tag1", null, null, "IOTEDGE_GATEWAYHOSTNAME", "parentaddress")]
        [InlineData("$upstream:/comp/ea:tag1", null, null, "IOTEDGE_GATEWAYHOSTNAME", "parentaddress")]
        [InlineData("$upstream:08/comp/ea:tag1", null, null, "IOTEDGE_GATEWAYHOSTNAME", "parentaddress")]
        public void ParseURITest(string image, string result, Type expectedException, string variableName, string value)
        {
            MockEnvironment mock_env = new MockEnvironment();
            mock_env.SetVariable(variableName, value);
            NestedEdgeParentUriParser edgeParentUrlParser = new NestedEdgeParentUriParser();

            if (expectedException != null)
            {
                Assert.Throws(expectedException, () => edgeParentUrlParser.ParseURI(image, mock_env));
            }
            else
            {
                Option<string> updatedImage = edgeParentUrlParser.ParseURI(image, mock_env);

                if (result == null)
                {
                    Assert.True(!updatedImage.HasValue);
                }
                else
                {
                    Assert.Equal(result, updatedImage.Expect(() => new InvalidOperationException("NestedEdgeParentUrlParser should have returned a value")));
                }
            }
        }
    }
}
