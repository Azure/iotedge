// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public static class HubCoreEventIds
    {
        const int EventIdStart = 1000;
        public const int ConnectionManager = EventIdStart;
        public const int DeviceListener = EventIdStart + 100;
        public const int CloudEndpoint = EventIdStart + 200;
        public const int ModuleEndpoint = EventIdStart + 300;
        public const int Authenticator = EventIdStart + 400;
    }
}