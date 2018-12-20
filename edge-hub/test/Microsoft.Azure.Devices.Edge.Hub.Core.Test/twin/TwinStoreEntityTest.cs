// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.twin
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Twin;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class TwinStoreEntityTest
    {
        [Fact]
        public void RoundtripEmptyTest()
        {
            var twinStoreEntity = new TwinStoreEntity();
            string json = JsonConvert.SerializeObject(twinStoreEntity);
            var deserializedObject = JsonConvert.DeserializeObject<TwinStoreEntity>(json);
            Assert.False(deserializedObject.Twin.HasValue);
            Assert.False(deserializedObject.ReportedPropertiesPatch.HasValue);
        }

        [Fact]
        public void RoundtripReportedPropertiesPatchTest()
        {
            var reportedProperties = new TwinCollection();
            reportedProperties["P1"] = "v1";
            reportedProperties["P2"] = "v2";

            var twinStoreEntity = new TwinStoreEntity(reportedProperties);
            string json = JsonConvert.SerializeObject(twinStoreEntity);
            var deserializedObject = JsonConvert.DeserializeObject<TwinStoreEntity>(json);

            Assert.False(deserializedObject.Twin.HasValue);
            Assert.True(deserializedObject.ReportedPropertiesPatch.HasValue);
            Assert.Equal("v1", (string)deserializedObject.ReportedPropertiesPatch.OrDefault()["P1"]);
            Assert.Equal("v2", (string)deserializedObject.ReportedPropertiesPatch.OrDefault()["P2"]);
        }

        [Fact]
        public void RoundtripTwinTest()
        {
            var reportedProperties = new TwinCollection();
            reportedProperties["P1"] = "v1";
            var desiredProperties = new TwinCollection();
            desiredProperties["P2"] = "v2";
            var twin = new Twin(new TwinProperties {Desired = desiredProperties, Reported = reportedProperties});
            var twinStoreEntity = new TwinStoreEntity(twin);
            string json = JsonConvert.SerializeObject(twinStoreEntity);
            var deserializedObject = JsonConvert.DeserializeObject<TwinStoreEntity>(json);

            Assert.False(deserializedObject.ReportedPropertiesPatch.HasValue);
            Assert.True(deserializedObject.Twin.HasValue);            
            Assert.Equal("v1", (string)deserializedObject.Twin.OrDefault().Properties.Reported["P1"]);
            Assert.Equal("v2", (string)deserializedObject.Twin.OrDefault().Properties.Desired["P2"]);
        }
    }
}
