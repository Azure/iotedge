// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DockerUtil
    {
        public static bool TryParseHostnameFromImage(string image, out string hostname)
        {
            hostname = string.Empty;
            int separator = Preconditions.CheckNotNull<string>(image).IndexOf('/');
            if (separator == -1)
            {
                return false;
            }

            hostname = image.Substring(0, separator);
            return true;
        }

        public static AuthConfig FirstAuthConfigOrDefault(string image, IEnumerable<AuthConfig> authConfigs)
        {
            Preconditions.CheckNotNull<string>(image);
            return authConfigs.FirstOrDefault(
                auth =>
                {
                    string hostname;
                    return TryParseHostnameFromImage(image, out hostname) ?
                        string.Compare(hostname, auth.ServerAddress, StringComparison.OrdinalIgnoreCase) == 0 :
                        false;
                });

        }
    }
}