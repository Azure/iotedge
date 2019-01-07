// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.twin
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Twin;
    using Microsoft.Azure.Devices.Edge.Storage;
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

        [Fact]
        public void BackwardCompatTest()
        {
            // Arrange
            var twinReportedProperties = new TwinCollection();
            twinReportedProperties["P1"] = "v1";
            var twinDesiredProperties = new TwinCollection();
            twinDesiredProperties["P2"] = "v2";
            var twin = new Twin(new TwinProperties { Desired = twinDesiredProperties, Reported = twinReportedProperties });

            var reportedProperties = new TwinCollection();
            reportedProperties["r1"] = "rv1";

            // Act
            var twinInfo1 = new TwinInfo(twin, reportedProperties);
            string json1 = twinInfo1.ToJson();
            var twinStoreEntity1 = json1.FromJson<TwinStoreEntity>();

            // Assert
            Assert.NotNull(twinStoreEntity1);
            Assert.True(twinStoreEntity1.Twin.HasValue);
            Assert.Equal("v1", (string)twinStoreEntity1.Twin.OrDefault().Properties.Reported["P1"]);
            Assert.Equal("v2", (string)twinStoreEntity1.Twin.OrDefault().Properties.Desired["P2"]);
            Assert.True(twinStoreEntity1.ReportedPropertiesPatch.HasValue);
            Assert.Equal("rv1", (string)twinStoreEntity1.ReportedPropertiesPatch.OrDefault()["r1"]);

            // Act
            var twinInfo2 = new TwinInfo(twin, null);
            string json2 = twinInfo2.ToJson();
            var twinStoreEntity2 = json2.FromJson<TwinStoreEntity>();

            // Assert
            Assert.NotNull(twinStoreEntity2);
            Assert.True(twinStoreEntity2.Twin.HasValue);
            Assert.Equal("v1", (string)twinStoreEntity2.Twin.OrDefault().Properties.Reported["P1"]);
            Assert.Equal("v2", (string)twinStoreEntity2.Twin.OrDefault().Properties.Desired["P2"]);
            Assert.False(twinStoreEntity2.ReportedPropertiesPatch.HasValue);

            // Act
            var twinInfo3 = new TwinInfo(null, reportedProperties);
            string json3 = twinInfo3.ToJson();
            var twinStoreEntity3 = json3.FromJson<TwinStoreEntity>();

            // Assert
            Assert.NotNull(twinStoreEntity3);
            Assert.False(twinStoreEntity3.Twin.HasValue);
            Assert.True(twinStoreEntity3.ReportedPropertiesPatch.HasValue);
            Assert.Equal("rv1", (string)twinStoreEntity3.ReportedPropertiesPatch.OrDefault()["r1"]);

            // Act
            var twinInfo4 = new TwinInfo(null, null);
            string json4 = twinInfo4.ToJson();
            var twinStoreEntity4 = json4.FromJson<TwinStoreEntity>();

            // Assert
            Assert.NotNull(twinStoreEntity4);
            Assert.False(twinStoreEntity4.Twin.HasValue);
            Assert.False(twinStoreEntity4.ReportedPropertiesPatch.HasValue);
        }
    }
}
