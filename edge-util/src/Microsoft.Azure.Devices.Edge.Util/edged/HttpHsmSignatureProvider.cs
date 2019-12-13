// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;
    using System.Threading.Tasks;

    public class HttpHsmSignatureProvider : ISignatureProvider
    {
        const string DefaultSignRequestAlgo = "HMACSHA256";
        const string DefaultKeyId = "primary";

        readonly WorkloadClient workloadClient;

        public HttpHsmSignatureProvider(string moduleId, string generationId, string providerUri, string apiVersion, string clientApiVersion)
        {
            Preconditions.CheckNotNull(providerUri, nameof(providerUri));
            Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion));
            Preconditions.CheckNonWhiteSpace(clientApiVersion, nameof(clientApiVersion));
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));

            this.workloadClient = new WorkloadClient(new Uri(providerUri), apiVersion, clientApiVersion, moduleId, generationId);
        }

        public Task<string> SignAsync(string data)
        {
            try
            {
                return this.workloadClient.SignAsync(DefaultKeyId, DefaultSignRequestAlgo, data);
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case WorkloadCommunicationException errorResponseException:
                        throw new HttpHsmCommunicationException(errorResponseException.Message, errorResponseException.StatusCode);
                    default:
                        throw;
                }
            }
        }
    }
}
