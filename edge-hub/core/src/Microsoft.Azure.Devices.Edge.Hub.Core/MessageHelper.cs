// Copyright (c) Microsoft. All rights reserved.

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

        public static string GetOutput(this IMessage message)
        {
            if (message.SystemProperties.TryGetValue(SystemProperties.OutputName, out string outputName))
            {
                return outputName;
            }

            return string.Empty;
        }

        public static string GetInput(this IMessage message)
        {
            if (message.SystemProperties.TryGetValue(SystemProperties.InputName, out string outputName))
            {
                return outputName;
            }

            return string.Empty;
        }

        public static string GetSenderId(this Devices.Routing.Core.IMessage routingMessage)
        {
            if (routingMessage.SystemProperties.TryGetValue(SystemProperties.ConnectionDeviceId, out string deviceId))
            {
                return routingMessage.SystemProperties.TryGetValue(SystemProperties.ConnectionModuleId, out string moduleId)
                    ? $"{deviceId}/{moduleId}"
                    : deviceId;
            }

            return string.Empty;
        }

        public static string GetOutput(this Devices.Routing.Core.IMessage routingMessage)
        {
            if (routingMessage.SystemProperties.TryGetValue(SystemProperties.OutputName, out string outputName))
            {
                return outputName;
            }

            return string.Empty;
        }
    }
}
