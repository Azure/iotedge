// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Edged
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.WorkloadTestServer;
    using Xunit;
    using TestCertificateHelper = Microsoft.Azure.Devices.Edge.Util.Test.Common.CertificateHelper;

    [Unit]
    public class WorkloadClientTest : IClassFixture<WorkloadFixture>
    {
        const string ModuleId = "testModule";
        const string ModulegenerationId = "1";
        const string Iv = "7826a0b7e6de4a84bd39c8e69c46d2a6";
        readonly Uri serverUri;

        public WorkloadClientTest(WorkloadFixture workloadFixture)
        {
            this.serverUri = new Uri(workloadFixture.ServiceUrl);
        }

        [Fact]
        public void VersioningTest()
        {
            string workloadApiVersion = "2018-06-28";
            string workloadClientApiVersion = "2018-06-28";

            var client = new WorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId).GetVersionedWorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId);
            Assert.True(client is Util.Edged.Version_2018_06_28.WorkloadClient);

            workloadApiVersion = "2018-06-28";
            workloadClientApiVersion = "2019-01-30";

            client = new WorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId).GetVersionedWorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId);
            Assert.True(client is Util.Edged.Version_2018_06_28.WorkloadClient);

            workloadApiVersion = "2019-01-30";
            workloadClientApiVersion = "2018-06-28";

            client = new WorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId).GetVersionedWorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId);
            Assert.True(client is Util.Edged.Version_2018_06_28.WorkloadClient);

            workloadApiVersion = "2019-01-30";
            workloadClientApiVersion = "2019-01-30";

            client = new WorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId).GetVersionedWorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId);
            Assert.True(client is Util.Edged.Version_2019_01_30.WorkloadClient);
        }

        [Theory]
        [InlineData("2018-06-28", "2018-06-28")]
        [InlineData("2018-06-28", "2019-01-30")]
        [InlineData("2019-01-30", "2018-06-28")]
        [InlineData("2019-01-30", "2019-01-30")]
        public void InstantiateInvalidArgumentsShouldThrow(string workloadApiVersion, string workloadClientApiVersion)
        {
            Assert.Throws<ArgumentNullException>(() => new WorkloadClient(null, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId));
            Assert.Throws<ArgumentException>(() => new WorkloadClient(this.serverUri, null, workloadClientApiVersion, ModuleId, ModulegenerationId));
            Assert.Throws<ArgumentException>(() => new WorkloadClient(this.serverUri, string.Empty, workloadClientApiVersion, ModuleId, ModulegenerationId));
            Assert.Throws<ArgumentException>(() => new WorkloadClient(this.serverUri, workloadApiVersion, null, ModuleId, ModulegenerationId));
            Assert.Throws<ArgumentException>(() => new WorkloadClient(this.serverUri, workloadApiVersion, string.Empty, ModuleId, ModulegenerationId));
            Assert.Throws<ArgumentException>(() => new WorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, string.Empty, ModulegenerationId));
            Assert.Throws<ArgumentException>(() => new WorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, string.Empty));
        }

        [Theory]
        [InlineData("2018-06-28", "2018-06-28")]
        [InlineData("2018-06-28", "2019-01-30")]
        [InlineData("2019-01-30", "2018-06-28")]
        [InlineData("2019-01-30", "2019-01-30")]
        public async Task GetServerCertificatesFromEdgeletShouldReturnCert(string workloadApiVersion, string workloadClientApiVersion)
        {
            var workloadClient = new WorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId);
            var response = await workloadClient.CreateServerCertificateAsync("hostname", DateTime.UtcNow.AddDays(1));

            Assert.Equal(new X509Certificate2(Encoding.UTF8.GetBytes(TestCertificateHelper.CertificatePem)), new X509Certificate2(Encoding.UTF8.GetBytes(response.Certificate)));
            Assert.Equal(TestCertificateHelper.PrivateKeyPem, response.PrivateKey);
        }

        [Theory]
        [InlineData("2018-06-28", "2018-06-28")]
        [InlineData("2018-06-28", "2019-01-30")]
        [InlineData("2019-01-30", "2018-06-28")]
        [InlineData("2019-01-30", "2019-01-30")]
        public async Task EncryptShouldReturnEncryptedTest(string workloadApiVersion, string workloadClientApiVersion)
        {
            var workloadClient = new WorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId);
            string response = await workloadClient.EncryptAsync(Iv, "text");

            string encrypted = Iv + "text";
            Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(encrypted)), response);
        }

        [Theory]
        [InlineData("2018-06-28", "2018-06-28")]
        [InlineData("2018-06-28", "2019-01-30")]
        [InlineData("2019-01-30", "2018-06-28")]
        [InlineData("2019-01-30", "2019-01-30")]
        public async Task DecryptShouldReturnEncryptedTest(string workloadApiVersion, string workloadClientApiVersion)
        {
            var workloadClient = new WorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId);
            string response = await workloadClient.DecryptAsync(Iv, Convert.ToBase64String(Encoding.UTF8.GetBytes("text")));

            string expected = Iv + "text";
            Assert.Equal(expected, response);
        }

        [Theory]
        [InlineData("2018-06-28", "2018-06-28")]
        [InlineData("2018-06-28", "2019-01-30")]
        [InlineData("2019-01-30", "2018-06-28")]
        [InlineData("2019-01-30", "2019-01-30")]
        public async Task SignAsync(string workloadApiVersion, string workloadClientApiVersion)
        {
            string payload = "some text";
            var data = Encoding.UTF8.GetBytes(payload);
            var workload = new WorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId);

            string response = await workload.SignAsync("key", "HMACSHA256", payload);

            string expected;
            using (var algorithm = new HMACSHA256(Encoding.UTF8.GetBytes("key")))
            {
                expected = Convert.ToBase64String(algorithm.ComputeHash(data));
            }

            Assert.Equal(expected, response);
        }

        [Theory]
        [InlineData("2018-06-28", "2018-06-28")]
        [InlineData("2018-06-28", "2019-01-30")]
        [InlineData("2019-01-30", "2018-06-28")]
        [InlineData("2019-01-30", "2019-01-30")]
        public async Task GetTrustBundleAsyncShouldReturnCert(string workloadApiVersion, string workloadClientApiVersion)
        {
            var workloadClient = new WorkloadClient(this.serverUri, workloadApiVersion, workloadClientApiVersion, ModuleId, ModulegenerationId);
            string response = await workloadClient.GetTrustBundleAsync();
            Assert.Equal(new X509Certificate2(Encoding.UTF8.GetBytes(TestCertificateHelper.CertificatePem)), new X509Certificate2(Encoding.UTF8.GetBytes(response)));
        }
    }
}
