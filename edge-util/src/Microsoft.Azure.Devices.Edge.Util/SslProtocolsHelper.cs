// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Authentication;
    using System.Text;
    using Microsoft.Extensions.Logging;

    public static class SslProtocolsHelper
    {
        // 
        public static SslProtocols Parse(string protocols, SslProtocols defaultSslProtocols, ILogger logger)
        {
            if (string.IsNullOrEmpty(protocols))
            {
                return defaultSslProtocols;
            }
            else
            {
                List<string> protocolStrings = protocols.Split(',').Select(s => s.Replace(".", string.Empty).Trim()).ToList();
                var sslProtocols = new List<SslProtocols>();
                foreach (string protocolString in protocolStrings)
                {
                    if (!Enum.TryParse(protocolString, true, out SslProtocols sslProtocol))
                    {
                        logger?.LogWarning($"Unable to parse SSLProtocol {protocolString}");
                    }
                    else
                    {
                        sslProtocols.Add(sslProtocol);
                    }
                }

                if (sslProtocols.Count == 0)
                {
                    return defaultSslProtocols;
                }

                SslProtocols combinedSslProtocols = sslProtocols.Aggregate(SslProtocols.None, (current, bt) => current | bt);
                return combinedSslProtocols;
            }
        }

        public static string Print(this SslProtocols sslProtocols)
        {
            var sslProtocolsList = new List<string>();
            if ((sslProtocols & SslProtocols.Ssl2) > 0)
            {
                sslProtocolsList.Add($"{SslProtocols.Ssl2}");
            }

            if ((sslProtocols & SslProtocols.Ssl3) > 0)
            {
                sslProtocolsList.Add($"{SslProtocols.Ssl3}");
            }

            if ((sslProtocols & SslProtocols.Tls) > 0)
            {
                sslProtocolsList.Add($"{SslProtocols.Tls}");
            }

            if ((sslProtocols & SslProtocols.Tls11) > 0)
            {
                sslProtocolsList.Add($"{SslProtocols.Tls11}");
            }

            if ((sslProtocols & SslProtocols.Tls12) > 0)
            {
                sslProtocolsList.Add($"{SslProtocols.Tls12}");
            }

            return sslProtocolsList.Count > 0 ? string.Join(", ", sslProtocolsList) : $"{SslProtocols.None}";
        }
    }
}
