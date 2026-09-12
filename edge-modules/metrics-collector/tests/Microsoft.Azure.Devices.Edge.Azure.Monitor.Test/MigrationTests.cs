using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Monitor.Ingestion;
using Microsoft.Azure.Devices.Edge.Azure.Monitor.FixedSetTableUpload;
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