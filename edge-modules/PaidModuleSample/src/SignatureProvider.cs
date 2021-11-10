// Copyright (c) Microsoft. All rights reserved.
namespace PaidModuleSample
{
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;

    public class SignatureProvider
    {
        const string DefaultKeyId = "primary";
        const string ApiVersion = "2020-10-10";

        readonly Uri WorkloadUri;
        readonly string ModuleId;
        readonly string GenerationId;

        public SignatureProvider(string moduleId, string generationId, string providerUri)
        {
            this.WorkloadUri = new Uri(providerUri);
            this.ModuleId = moduleId;
            this.GenerationId = generationId;
        }

        public async Task<string> SignAsync(string data)
        {
            var signRequest = new SignRequest
            {
                KeyId = DefaultKeyId,
                Algo = SignRequestAlgo.HMACSHA256,
                Data = Encoding.UTF8.GetBytes(data)
            };

            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.WorkloadUri))
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.WorkloadUri) };
                SignResponse response = await edgeletHttpClient.SignAsync(ApiVersion, this.ModuleId, this.GenerationId, signRequest);
                return Convert.ToBase64String(response.Digest);
            }
        }

        public async Task<string> GetTrustBundleAsync()
        {
          
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.WorkloadUri))
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.WorkloadUri) };
                var response = await edgeletHttpClient.TrustBundleAsync(ApiVersion);
                return response.Certificate;
            }
        }
    }
}
