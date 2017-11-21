// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding.Config
{
    using System;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Host;
    using Microsoft.Azure.WebJobs.Host.Config;

    /// <summary>
    /// Extension methods for EdgeHub integration
    /// </summary>
    public static class EdgeHubHostConfigExtensions
    {
        /// <summary>
        /// Enables use of EdgeHub binding extensions
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        public static void UseEdgeHub(this JobHostConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            
            var extensions = config.GetService<IExtensionRegistry>();

            var extensionConfig = new EdgeHubExtensionConfigProvider();
            extensions.RegisterExtension<IExtensionConfigProvider>(extensionConfig);
        }
    }
}
