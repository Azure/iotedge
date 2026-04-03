// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub.Config
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.WebJobs.Description;
    using Microsoft.Azure.WebJobs.Host.Bindings;
    using Microsoft.Azure.WebJobs.Host.Config;
    using Newtonsoft.Json;

    /// <summary>
    /// Extension configuration provider used to register EdgeHub triggers and binders
    /// </summary>
    [Extension("EdgeHub")]
    class EdgeHubExtensionConfigProvider : IExtensionConfigProvider
    {
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var bindingProvider = new EdgeHubTriggerBindingProvider();
            var rule = context.AddBindingRule<EdgeHubTriggerAttribute>();
            rule.AddConverter<IncomingMessage, string>(ConvertIncomingMessageToString);
            rule.AddConverter<IncomingMessage, byte[]>(ConvertIncomingMessageToBytes);
            rule.BindToTrigger<IncomingMessage>(bindingProvider);

            var rule2 = context.AddBindingRule<EdgeHubAttribute>();
            rule2.BindToCollector<TelemetryMessage>(typeof(EdgeHubCollectorBuilder));
            rule2.AddConverter<string, TelemetryMessage>(ConvertStringToMessage);
            rule2.AddConverter<byte[], TelemetryMessage>(ConvertBytesToMessage);
            rule2.AddOpenConverter<OpenType.Poco, TelemetryMessage>(this.ConvertPocoToMessage);
        }

        static TelemetryMessage ConvertBytesToMessage(byte[] msgBytes) => new TelemetryMessage(msgBytes);

        static TelemetryMessage ConvertStringToMessage(string msg) => ConvertBytesToMessage(Encoding.UTF8.GetBytes(msg));

        static byte[] ConvertIncomingMessageToBytes(IncomingMessage msg) => msg.Payload;

        static string ConvertIncomingMessageToString(IncomingMessage msg) => Encoding.UTF8.GetString(msg.Payload);

        Task<object> ConvertPocoToMessage(object src, Attribute attribute, ValueBindingContext context) => Task.FromResult<object>(ConvertStringToMessage(JsonConvert.SerializeObject(src)));
    }
}
