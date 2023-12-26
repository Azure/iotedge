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
        //      Tls12, Tls13
        //      Tls1.2, Tls1.3
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

            if ((sslProtocols & SslProtocols.Tls12) > 0)
            {
                sslProtocolsList.Add($"{SslProtocols.Tls12}");
            }

            if ((sslProtocols & SslProtocols.Tls13) > 0)
            {
                sslProtocolsList.Add($"{SslProtocols.Tls13}");
            }

            return sslProtocolsList.Count > 0 ? string.Join(", ", sslProtocolsList) : $"{SslProtocols.None}";
        }

        // Parses TLS protocol from a text representation.
        static bool TryParseProtocol(string protocol, out SslProtocols sslProtocol)
        {
            switch (protocol.ToLowerInvariant())
            {
                case "tls12":
                case "tls1.2":
                case "tls1_2":
                case "tlsv12":
                    sslProtocol = SslProtocols.Tls12;
                    return true;
                case "tls13":
                case "tls1.3":
                case "tls1_3":
                case "tlsv13":
                    sslProtocol = SslProtocols.Tls13;
                    return true;
            }

            sslProtocol = default(SslProtocols);
            return false;
        }
    }
}
