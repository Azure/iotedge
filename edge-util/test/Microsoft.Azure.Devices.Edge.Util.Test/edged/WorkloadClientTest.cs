// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Edged
{
    using System;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.Edged.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.WorkloadTestServer;
    using Xunit;
    using TestCertificateHelper = Microsoft.Azure.Devices.Edge.Util.Test.Common.CertificateHelper;

    [Unit]
    public class WorkloadClientTest : IClassFixture<WorkloadFixture>
    {
        readonly Uri serverUri;
        const string WorkloadApiVersion = "2018-06-28";
        const string ModuleId = "testModule";
        const string ModulegenerationId = "1";
        const string Iv = "7826a0b7e6de4a84bd39c8e69c46d2a6";

        public WorkloadClientTest(WorkloadFixture workloadFixture)
        {
            this.serverUri = new Uri(workloadFixture.ServiceUrl);
        }

        [Fact]
        public void InstantiateInvalidArgumentsShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new WorkloadClient(null, WorkloadApiVersion, ModuleId, ModulegenerationId));
            Assert.Throws<ArgumentException>(() => new WorkloadClient(this.serverUri, "", ModuleId, ModulegenerationId));
            Assert.Throws<ArgumentException>(() => new WorkloadClient(this.serverUri, WorkloadApiVersion, "", ModulegenerationId));
            Assert.Throws<ArgumentException>(() => new WorkloadClient(this.serverUri, WorkloadApiVersion, ModuleId, ""));
        }

        [Fact]
        public async Task GetServerCertificatesFromEdgeletShouldReturnCert()
        {
            var workloadClient = new WorkloadClient(this.serverUri, WorkloadApiVersion, ModuleId, ModulegenerationId);
            CertificateResponse response = await workloadClient.CreateServerCertificateAsync("hostname", DateTime.UtcNow.AddDays(1));

            Assert.Equal(new X509Certificate2(Encoding.UTF8.GetBytes(TestCertificateHelper.CertificatePem)), new X509Certificate2(Encoding.UTF8.GetBytes(response.Certificate)));
            Assert.Equal(TestCertificateHelper.PrivateKeyPem, response.PrivateKey.Bytes);
        }

        [Fact]
        public async Task EncryptShouldReturnEncryptedTest()
        {
            var workloadClient = new WorkloadClient(this.serverUri, WorkloadApiVersion, ModuleId, ModulegenerationId);
            string response = await workloadClient.EncryptAsync(Iv, "text");

            string encrypted = Iv + "text";
            Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(encrypted)), response);
        }

        [Fact]
        public async Task DecryptShouldReturnEncryptedTest()
        {
            var workloadClient = new WorkloadClient(this.serverUri, WorkloadApiVersion, ModuleId, ModulegenerationId);
            string response = await workloadClient.DecryptAsync(Iv, Convert.ToBase64String(Encoding.UTF8.GetBytes("text")));

            string expected = Iv + "text";
            Assert.Equal(expected, response);
        }

        [Fact]
        public async Task SignAsync()
        {
            byte[] data = Encoding.UTF8.GetBytes("some text");
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.serverUri))
            {
                var workload = new HttpWorkloadClient(httpClient)
                {
                    BaseUrl = HttpClientHelper.GetBaseUrl(this.serverUri)
                };

                var payload = new SignRequest()
                {
                    Algo = SignRequestAlgo.HMACSHA256,
                    Data = data,
                    KeyId = "primary"
                };
                SignResponse response = await workload.SignAsync(WorkloadApiVersion, "testModule", "1", payload);

                string expected;
                using (var algorithm = new HMACSHA256(Encoding.UTF8.GetBytes("key")))
                {
                    expected = Convert.ToBase64String(algorithm.ComputeHash(data));
                }

                Assert.Equal(expected, Convert.ToBase64String(response.Digest));
            }
        }

        [Fact]
        public async Task GetTrustBundleAsyncShouldReturnCert()
        {
            var workloadClient = new WorkloadClient(this.serverUri, WorkloadApiVersion, ModuleId, ModulegenerationId);
            var response = await workloadClient.GetTrustBundleAsync();
            Assert.Equal(new X509Certificate2(Encoding.UTF8.GetBytes(TestCertificateHelper.CertificatePem)), new X509Certificate2(Encoding.UTF8.GetBytes(response.Certificate)));
        }
    }
}
