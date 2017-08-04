// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding.Config
{
    using System;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Host;
    using Microsoft.Azure.WebJobs.Host.Config;
    using Microsoft.Azure.WebJobs.Host.Triggers;

    /// <summary>
    /// Extension configuration provider used to register EdgeHub triggers and binders
    /// </summary>
    public class EdgeHubExtensionConfigProvider : IExtensionConfigProvider
    {
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var extensions = context.Config.GetService<IExtensionRegistry>();
            var nameResolver = context.Config.GetService<INameResolver>();

            // register trigger binding provider
            var triggerBindingProvider = new EdgeHubTriggerBindingProvider(nameResolver);
            extensions.RegisterExtension<ITriggerBindingProvider>(triggerBindingProvider);

            context.AddConverter<Message, string>(MessageConverter);
        }

        string MessageConverter(Message arg)
        {
            return arg.ToString();
        }
    }
}
