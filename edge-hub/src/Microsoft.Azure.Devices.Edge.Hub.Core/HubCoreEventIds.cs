// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public static class HubCoreEventIds
    {
        public const int Authenticator = EventIdStart + 400;
        public const int CloudEndpoint = EventIdStart + 200;
        public const int ConfigUpdater = EventIdStart + 800;
        public const int ConnectionManager = EventIdStart;
        public const int DeviceListener = EventIdStart + 100;
        public const int DeviceScopeIdentitiesCache = EventIdStart + 1200;
        public const int EdgeHubConnection = EventIdStart + 900;
        public const int InvokeMethodHandler = EventIdStart + 1100;
        public const int MessageStore = EventIdStart + 600;
        public const int ModuleEndpoint = EventIdStart + 300;
        public const int PeriodicConnectionAuthenticator = EventIdStart + 1300;
        public const int RoutingEdgeHub = EventIdStart + 500;
        public const int TokenCredentialsStore = EventIdStart + 1000;
        public const int TwinManager = EventIdStart + 700;
        const int EventIdStart = 1000;
    }
}
