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
            var nameResolver = config.GetService<INameResolver>();

            var extensionConfig = new EdgeHubExtensionConfig(nameResolver);
            extensions.RegisterExtension<IExtensionConfigProvider>(extensionConfig);
        }

        class EdgeHubExtensionConfig : IExtensionConfigProvider
        {
            readonly INameResolver nameResolver;

            public EdgeHubExtensionConfig(INameResolver nameResolver)
            {
                this.nameResolver = nameResolver;
            }

            public void Initialize(ExtensionConfigContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                // Register our extension binding providers
                context.Config.RegisterBindingExtensions(
                    new EdgeHubTriggerBindingProvider(this.nameResolver));
            }
        }
    }
}
