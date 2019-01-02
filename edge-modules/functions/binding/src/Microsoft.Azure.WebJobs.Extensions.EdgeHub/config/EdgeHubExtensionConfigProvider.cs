// Copyright (c) Microsoft. All rights reserved.
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub.Config
{
    using System;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.WebJobs.Description;
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
            rule.AddConverter<Message, string>(ConvertMessageToString);
            rule.AddConverter<Message, byte[]>(ConvertMessageToBytes);
            rule.BindToTrigger<Message>(bindingProvider);

            var rule2 = context.AddBindingRule<EdgeHubAttribute>();
            rule2.BindToCollector<Message>(typeof(EdgeHubCollectorBuilder));
            rule2.AddConverter<string, Message>(ConvertStringToMessage);
            rule2.AddConverter<byte[], Message>(ConvertBytesToMessage);
            rule2.AddOpenConverter<OpenType.Poco, Message>(ConvertPocoToMessage);
        }

        private Task<object> ConvertPocoToMessage(object src, Attribute attribute, ValueBindingContext context) => Task.FromResult<object>(ConvertStringToMessage(JsonConvert.SerializeObject(src)));

        private static Message ConvertBytesToMessage(byte[] msgBytes) => new Message(msgBytes);

        private static Message ConvertStringToMessage(string msg) => ConvertBytesToMessage(Encoding.UTF8.GetBytes(msg));

        private static byte[] ConvertMessageToBytes(Message msg) => msg.GetBytes();

        private static string ConvertMessageToString(Message msg) => Encoding.UTF8.GetString(ConvertMessageToBytes(msg));
    }
}
