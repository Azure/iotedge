// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class DockerUtil
    {
        /// <summary>
        /// This is the grammar for image names, from the docker codebase:
        /// Grammar
        ///
        ///   reference                       := name [ ":" tag ] [ "@" digest ]
        ///	  name                            := [domain '/'] path-component ['/' path-component]*
        ///	  domain                          := domain-component ['.' domain-component]* [':' port-number]
        ///	  domain-component                := /([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9])/
        ///	  port-number                     := /[0-9]+/
        ///	  path-component                  := alpha-numeric [separator alpha-numeric]*
        ///   alpha-numeric                   := /[a-z0-9]+/
        ///	  separator                       := /[_.]|__|[-]*/
        ///
        ///	  tag                             := /[\w][\w.-]{0,127}/
        ///
        ///	  digest                          := digest-algorithm ":" digest-hex
        ///	  digest-algorithm                := digest-algorithm-component [ digest-algorithm-separator digest-algorithm-component ]*
        ///	  digest-algorithm-separator      := /[+.-_]/
        ///	  digest-algorithm-component      := /[A-Za-z][A-Za-z0-9]*/
        ///	  digest-hex                      := /[0-9a-fA-F]{32,}/ ; At least 128 bit digest value
        ///
        ///	  identifier                      := /[a-f0-9]{64}/
        ///	  short-identifier                := /[a-f0-9]{6,64}/
        ///
        /// tl;dr if there is more than one path-component, and the first component contains a '.' or ':' then
        /// it is a registry address.
        /// For more information: https://github.com/docker/distribution/blob/master/reference/reference.go
        /// </summary>
        /// <param name="image"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static bool TryParseDomainFromImage(string image, out string domain)
        {
            domain = string.Empty;
            string[] parts = Preconditions.CheckNotNull(image).Split('/');
            if (parts.Length > 1)
            {
                string maybeDomain = parts[0];
                if (maybeDomain.Contains(".") || maybeDomain.Contains(":"))
                {
                    // Contains a '.' or ':' in the first component
                    // This must be a registry domain
                    domain = maybeDomain;
                    return true;
                }

                // if the first component is not a domain, it could still
                // refer to a private repository on docker hub.
                // return the default registry address
                domain = Constants.DefaultRegistryAddress;
                return true;
            }

            // Only one path-component in this image name
            // It must be a public repository on Docker Hub
            return false;
        }

        public static Option<AuthConfig> FirstAuthConfig(this IEnumerable<AuthConfig> authConfigs, string image)
        {
            Preconditions.CheckNotNull(image);
            return authConfigs == null
                ? Option.None<AuthConfig>()
                : authConfigs.FirstOption(auth => HostnameMatches(image, auth));
        }

        static bool HostnameMatches(string image, AuthConfig auth) =>
            TryParseDomainFromImage(image, out string hostname)
                ? string.Compare(hostname, auth.ServerAddress, StringComparison.OrdinalIgnoreCase) == 0
                : false;
    }
}
