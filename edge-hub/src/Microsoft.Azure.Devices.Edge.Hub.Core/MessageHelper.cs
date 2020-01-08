// Copyright (c) Microsoft. All rights reserved.
using System;

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public static class MessageHelper
    {
        public static string GetSenderId(this IMessage message)
        {
            if (message.SystemProperties.TryGetValue(SystemProperties.ConnectionDeviceId, out string deviceId))
            {
                return message.SystemProperties.TryGetValue(SystemProperties.ConnectionModuleId, out string moduleId)
                    ? $"{deviceId}/{moduleId}"
                    : deviceId;
            }

            return string.Empty;
        }

        public static void WriteRoute(this IMessage message)
        {
            Console.WriteLine("\nRoute:");
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(message.SystemProperties));
            if (message.SystemProperties.TryGetValue(SystemProperties.InputName, out string inputName))
            {
                Console.WriteLine(inputName);
            }

            if (message.SystemProperties.TryGetValue(SystemProperties.OutputName, out string outputName))
            {
                Console.WriteLine(outputName);
            }

            if (message.SystemProperties.TryGetValue(SystemProperties.OutboundUri, out string outputUri))
            {
                Console.WriteLine(outputUri);
            }
        }
    }
}
