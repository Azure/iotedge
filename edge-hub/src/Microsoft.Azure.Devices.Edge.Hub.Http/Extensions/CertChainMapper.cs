// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Extensions
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public static class CertChainMapper // TODO: Replace hacky POC
    {
        // static readonly ILogger Log = Logger.Factory.CreateLogger<CertChainMapper>();
        static ConcurrentDictionary<string, IList<X509Certificate2>> certsToChain = new ConcurrentDictionary<string, IList<X509Certificate2>>();

        public static void ImportCertChain(string thumbprint, X509ChainElementCollection chainElements)
        {
            IList<X509Certificate2> certChainCopy = new List<X509Certificate2>();
            foreach (X509ChainElement chainElement in chainElements)
            {
                certChainCopy.Add(new X509Certificate2(chainElement.Certificate));
            }

            certsToChain[thumbprint] = certChainCopy;
        }

        // TODO: log in the case where remove fails (there must be concurrent overlapping connections which shouldn't happen)
        public static Option<IList<X509Certificate2>> ExtractCertChain(ConnectionInfo connectionInfo)
        {
            if (connectionInfo.ClientCertificate != null)
            {
                X509Certificate2 clientCertificate = connectionInfo.ClientCertificate;
                IList<X509Certificate2> certChain;
                certsToChain.TryRemove(clientCertificate.Thumbprint, out certChain);

                if (certChain == null)
                {
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
