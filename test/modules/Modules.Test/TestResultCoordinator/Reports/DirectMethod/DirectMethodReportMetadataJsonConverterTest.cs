// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using global::TestResultCoordinator.Reports;
    using global::TestResultCoordinator.Reports.DirectMethod;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Newtonsoft.Json;
    using Xunit;

    public class DirectMethodReportMetadataJsonConverterTest
    {
        const string SerializedTestJsonWithReceiverSource = "{ \"TestReportType\": \"DirectMethodReport\", \"SenderSource\": \"directMethodSender1.send\"," +
            " \"ReceiverSource\": \"directMethodReceiver1.receive\", \"TolerancePeriod\": \"00:00:00.005\" }";
        const string SerializedTestJsonWithoutReceiverSource = "{ \"TestReportType\": \"DirectMethodReport\", \"SenderSource\": \"directMethodSender1.send\", " +
            "\"TolerancePeriod\": \"00:00:00.005\" }";

        [Fact]
        public void DirectMethodJsonConvertWitReceiverSourceTest()
        {
            DirectMethodReportMetadata directMethodReportMetadata =
                JsonConvert.DeserializeObject<DirectMethodReportMetadata>(SerializedTestJsonWithReceiverSource);
            Assert.Equal("directMethodSender1.send", directMethodReportMetadata.SenderSource);
            Assert.Equal(new TimeSpan(0, 0, 0, 0, 5), directMethodReportMetadata.TolerancePeriod);
            Assert.Equal(TestReportType.DirectMethodReport, directMethodReportMetadata.TestReportType);
            Assert.True(directMethodReportMetadata.ReceiverSource.HasValue);
            directMethodReportMetadata.ReceiverSource.ForEach(x => Assert.Equal("directMethodReceiver1.receive", x));
        }

        [Fact]
        public void DirectMethodJsonConvertWithoutReceiverSourceTest()
        {
            DirectMethodReportMetadata directMethodReportMetadata =
                JsonConvert.DeserializeObject<DirectMethodReportMetadata>(SerializedTestJsonWithoutReceiverSource);
            Assert.Equal("directMethodSender1.send", directMethodReportMetadata.SenderSource);
            Assert.Equal(new TimeSpan(0, 0, 0, 0, 5), directMethodReportMetadata.TolerancePeriod);
            Assert.Equal(TestReportType.DirectMethodReport, directMethodReportMetadata.TestReportType);
            Assert.True(!directMethodReportMetadata.ReceiverSource.HasValue);
        }
    }
}
