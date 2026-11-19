using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Core;
using Azure.Monitor.Ingestion;
using Microsoft.Azure.Devices.Edge.Azure.Monitor.FixedSetTableUpload;
using Moq;
using FixedSetTableUploadType = Microsoft.Azure.Devices.Edge.Azure.Monitor.FixedSetTableUpload.FixedSetTableUpload;
using Xunit;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.Test
{
    public class MigrationTests
    {
        [Theory]
        [InlineData("azure.com")]
        [InlineData("azure.us")]
        [InlineData("azure.cn")]
        [InlineData("azure.com.cn")]
        public void TestCloudConfiguration(string azureDomain)
        {
            (Uri authorityHost, LogsIngestionAudience audience) = AadCredentialFactory.GetCloudConfiguration(azureDomain);

            if (azureDomain == "azure.us")
            {
                Assert.Equal("https://login.microsoftonline.us/", authorityHost.ToString());
                Assert.Equal(LogsIngestionAudience.AzureGovernment, audience);
            }
            else if (azureDomain == "azure.cn" || azureDomain == "azure.com.cn")
            {
                Assert.Equal("https://login.chinacloudapi.cn/", authorityHost.ToString());
                Assert.Equal(LogsIngestionAudience.AzureChina, audience);
            }
            else
            {
                Assert.Equal("https://login.microsoftonline.com/", authorityHost.ToString());
                Assert.Equal(LogsIngestionAudience.AzurePublicCloud, audience);
            }
        }

        [Fact]
        public void TestCloudConfigurationRejectsUnsupportedDomain()
        {
            Assert.Throws<ArgumentException>(() => AadCredentialFactory.GetCloudConfiguration("azure.example"));
        }

        [Theory]
        [InlineData("azure.com", "https://my-dce.eastus-1.ingest.monitor.azure.com")]
        [InlineData("azure.com", "https://my-dcr.eastus-1.ingest.monitor.azure.com")]
        [InlineData("azure.com", "https://MY-DCE.EASTUS-1.INGEST.MONITOR.AZURE.COM.:443/")]
        [InlineData("azure.com", "https://prod.la.ingest.monitor.core.windows.net")]
        [InlineData("azure.com", "https://eastus.prod.la.ingest.monitor.core.windows.net")]
        [InlineData("azure.com", "https://prod.la.ingestion.msftcloudes.com")]
        [InlineData("azure.com", "https://eastus.prod.la.ingestion.msftcloudes.com")]
        [InlineData("azure.us", "https://my-dce.usgovvirginia-1.ingest.monitor.azure.us")]
        [InlineData("azure.cn", "https://my-dce.chinaeast2-1.ingest.monitor.azure.cn")]
        [InlineData("azure.com.cn", "https://my-dce.chinaeast2-1.ingest.monitor.azure.cn")]
        public void TestPublisherAcceptsOfficialIngestionEndpoint(string azureDomain, string endpoint)
        {
            var credential = new Mock<TokenCredential>(MockBehavior.Strict);
            LogsIngestionAudience audience = AadCredentialFactory.GetCloudConfiguration(azureDomain).Audience;

            new FixedSetTableUploadType(endpoint, "dcr-test", "Custom-Test_CL", "resource", credential.Object, audience);

            credential.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("azure.com", "https://receiver.example")]
        [InlineData("azure.com", "http://my-dce.eastus-1.ingest.monitor.azure.com")]
        [InlineData("azure.com", "https://my-dce.ingest.monitor.azure.com.receiver.example")]
        [InlineData("azure.com", "https://notingest.monitor.azure.com")]
        [InlineData("azure.com", "https://my-dce.ingest.monitor.azure.com@receiver.example")]
        [InlineData("azure.com", "https://receiver.example/ingest.monitor.azure.com")]
        [InlineData("azure.com", "https://127.0.0.1")]
        [InlineData("azure.com", "https://my-dce.usgovvirginia-1.ingest.monitor.azure.us")]
        [InlineData("azure.us", "https://prod.la.ingest.monitor.core.windows.net")]
        public void TestPublisherRejectsUnofficialEndpointBeforeAuthentication(string azureDomain, string endpoint)
        {
            var credential = new Mock<TokenCredential>(MockBehavior.Strict);
            LogsIngestionAudience audience = AadCredentialFactory.GetCloudConfiguration(azureDomain).Audience;

            Assert.Throws<ArgumentException>(() =>
                new FixedSetTableUploadType(endpoint, "dcr-test", "Custom-Test_CL", "resource", credential.Object, audience));

            credential.VerifyNoOtherCalls();
        }

        [Fact]
        public void TestFilterFiniteMetrics()
        {
            DateTime timestamp = DateTime.UnixEpoch;
            var metrics = new List<Metric>
            {
                new Metric(timestamp, "finite", 1, new Dictionary<string, string>()),
                new Metric(timestamp, "nan", double.NaN, new Dictionary<string, string>()),
                new Metric(timestamp, "positiveInfinity", double.PositiveInfinity, new Dictionary<string, string>()),
                new Metric(timestamp, "negativeInfinity", double.NegativeInfinity, new Dictionary<string, string>()),
            };

            List<Metric> finiteMetrics = FixedSetTableUploadType.FilterFiniteMetrics(metrics, out int skipped);

            Assert.Single(finiteMetrics);
            Assert.Equal("finite", finiteMetrics.Single().Name);
            Assert.Equal(3, skipped);
        }
    }
}