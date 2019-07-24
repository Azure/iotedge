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
    }
}
