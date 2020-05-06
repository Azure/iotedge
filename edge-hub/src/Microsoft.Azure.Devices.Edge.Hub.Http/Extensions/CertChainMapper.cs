// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Extensions
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class CertChainMapper
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<CertChainMapper>();
        readonly ConcurrentDictionary<string, IList<X509Certificate2>> certsToChain = new ConcurrentDictionary<string, IList<X509Certificate2>>();

        public CertChainMapper()
        {
        }

        public void ImportCertChain(string thumbprint, X509ChainElementCollection chainElements)
        {
            IList<X509Certificate2> certChainCopy = new List<X509Certificate2>();
            foreach (X509ChainElement chainElement in chainElements)
            {
                certChainCopy.Add(new X509Certificate2(chainElement.Certificate));
            }

            this.certsToChain[thumbprint] = certChainCopy;
        }

        public Option<IList<X509Certificate2>> ExtractCertChain(ConnectionInfo connectionInfo)
        {
            if (connectionInfo.ClientCertificate != null)
            {
                X509Certificate2 clientCertificate = connectionInfo.ClientCertificate;
                IList<X509Certificate2> certChain;
                this.certsToChain.TryRemove(clientCertificate.Thumbprint, out certChain);

                if (certChain == null)
                {
                    Log.LogError($"Did not find cert chain for connection {connectionInfo.Id}");
                    return Option.None<IList<X509Certificate2>>();
                }
                else
                {
                    return Option.Some(certChain);
                }
            }

            return Option.None<IList<X509Certificate2>>();
        }
    }
}
