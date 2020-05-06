// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Authentication;
    using Microsoft.Extensions.Logging;

    public static class SslProtocolsHelper
    {
        // Parse SslProtocols from input comma separated string
        // Examples:
        //      Tls1.2
        //      Tls12
        //      Tls11, Tls12
        //      Tls1.1, Tls1.2
        public static SslProtocols Parse(string protocols, SslProtocols defaultSslProtocols, ILogger logger)
        {
            if (string.IsNullOrEmpty(protocols))
            {
                return defaultSslProtocols;
            }

            var sslProtocols = new List<SslProtocols>();
            foreach (string protocolString in protocols.Split(',').Select(s => s.Trim()))
            {
                if (!TryParseProtocol(protocolString, out SslProtocols sslProtocol))
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

            return sslProtocols.Aggregate(SslProtocols.None, (current, bt) => current | bt);
        }

        // Print the SSL protocols included in this value (which is an or of multiple SSL protocol values)
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

        // Parses TLS protocol from a text representation.
        static bool TryParseProtocol(string protocol, out SslProtocols sslProtocol)
        {
            switch (protocol.ToLowerInvariant())
            {
                case "tls":
                case "tls1":
                case "tls10":
                case "tls1.0":
                case "tls1_0":
                case "tlsv10":
                    sslProtocol = SslProtocols.Tls;
                    return true;
                case "tls11":
                case "tls1.1":
                case "tls1_1":
                case "tlsv11":
                    sslProtocol = SslProtocols.Tls11;
                    return true;
                case "tls12":
                case "tls1.2":
                case "tls1_2":
                case "tlsv12":
                    sslProtocol = SslProtocols.Tls12;
                    return true;
            }

            sslProtocol = default(SslProtocols);
            return false;
        }
    }
}
