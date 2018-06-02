// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Edged
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Edged.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.WorkloadTestServer;
    using Xunit;
    using CertificateHelper = Microsoft.Azure.Devices.Edge.Util.CertificateHelper;
    using TestCertificateHelper = Microsoft.Azure.Devices.Edge.Util.Test.Common.CertificateHelper;

    [Unit]
    public class HttpWorkloadClientTest : IClassFixture<WorkloadFixture>
    {
        readonly Uri serverUri;
        const string WorkloadApiVersion = "2018-06-28";

        public HttpWorkloadClientTest(WorkloadFixture workloadFixture)
        {
            this.serverUri = new Uri(workloadFixture.ServiceUrl);
        }

        [Fact]
        public async Task GetServerCertificatesFromEdgeletShouldReturnCert()
        {
            (X509Certificate2 cert, IEnumerable<X509Certificate2> chain) = await CertificateHelper.GetServerCertificatesFromEdgelet(this.serverUri, WorkloadApiVersion, "testModule", "1", "hostname", DateTime.UtcNow.AddDays(1));

            var expected = new X509Certificate2(Encoding.UTF8.GetBytes(TestCertificateHelper.CertificatePem));
            Assert.Equal(expected, cert);
            Assert.True(cert.HasPrivateKey);
            Assert.Equal(chain.Count(), 1);
            Assert.Equal(expected, chain.First());
        }

        [Fact]
        public async Task GetServerCertificatesFromEdgeletEmptyHostnameShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CertificateHelper.GetServerCertificatesFromEdgelet(this.serverUri, WorkloadApiVersion, "testModule", "1", "", DateTime.UtcNow.AddDays(1)));
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
    }
}
