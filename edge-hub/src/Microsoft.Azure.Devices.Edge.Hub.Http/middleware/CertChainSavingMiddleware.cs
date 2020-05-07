// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Middleware
{
    using System.Collections.Generic;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Connections;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;

    public class CertChainSavingMiddleware
    {
        readonly ConnectionDelegate next;

        public CertChainSavingMiddleware(ConnectionDelegate next)
        {
            this.next = next;
        }

        public async Task OnConnectionAsync(ConnectionContext context)
        {
            await Task.Yield();

            SslStream sslStream = context.Features.Get<SslStream>();
            IList<X509Certificate2> certChainCopy = new List<X509Certificate2>();

            // TODO: should we leave inner stream open?
            new SslStream(sslStream, true, (_, clientCert, chain, policyErrors) =>
            {
                foreach (X509ChainElement chainElement in chain.ChainElements)
                {
                    certChainCopy.Add(new X509Certificate2(chainElement.Certificate));
                }

                return true;
            });

            TlsConnectionFeatureExtended tlsConnectionFeatureExtended = new TlsConnectionFeatureExtended
            {
                ChainElements = certChainCopy
            };
            context.Features.Set<ITlsConnectionFeatureExtended>(tlsConnectionFeatureExtended);
        }

        static class Events
        {
        }
    }
}
