// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Extensions
{
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;

    public static class CertChainMapper // TODO: Replace hacky POC
    {
        static Dictionary<string, IList<X509Certificate2>> certsToChain = new Dictionary<string, IList<X509Certificate2>>();

        public static void ImportCertChain(string thumbprint, X509ChainElementCollection chainElements)
        {
            IList<X509Certificate2> certChainCopy = new List<X509Certificate2>();
            foreach (X509ChainElement chainElement in chainElements)
            {
                certChainCopy.Add(new X509Certificate2(chainElement.Certificate));
            }

            certsToChain[thumbprint] = certChainCopy;
        }

        public static IList<X509Certificate2> ExtractCertChain(string thumbprint)
        {
            IList<X509Certificate2> certChain = certsToChain[thumbprint];
            certsToChain.Remove(thumbprint);
            return certChain;
        }
    }
}
