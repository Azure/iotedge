// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Net;
    using Microsoft.Extensions.Logging;

    public static class Proxy
    {
        public static Option<IWebProxy> Parse(string proxyUri, ILogger logger)
        {
            Preconditions.CheckNotNull(logger, nameof(logger));

            if (string.IsNullOrWhiteSpace(proxyUri))
            {
                return Option.None<IWebProxy>();
            }

            var uri = new Uri(proxyUri);
            if (string.IsNullOrEmpty(uri.UserInfo))
            {
                // http://proxyserver:1234
                logger.LogInformation($"Detected proxy {uri}");
                return Option.Some<IWebProxy>(new WebProxy(uri));
            }
            else
            {
                string[] parts = uri.UserInfo.Split(':');
                var credentials = new NetworkCredential
                {
                    UserName = Uri.UnescapeDataString(parts[0])
                };

                if (parts.Length == 1)
                {
                    // http://user@proxyserver:1234
                    logger.LogInformation($"Detected proxy {uri}");
                }
                else
                {
                    // http://user:password@proxyserver:1234
                    credentials.Password = Uri.UnescapeDataString(parts[1]);
                    logger.LogInformation($"Detected proxy {uri.ToString().Replace($":{parts[1]}@", ":****@")}");
                }

                var proxy = new WebProxy(uri)
                {
                    Credentials = credentials
                };

                return Option.Some<IWebProxy>(proxy);
            }
        }
    }
}
