// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class ConnectionInfo : IConnectionInfo
    {
        public Option<IIdentity> KnownParent { get; private set; }

        public ConnectionInfo(bool isDirectClient)
        {
            this.IsDirectClient = isDirectClient;
        }

        public bool IsDirectClient { get; }

        public void BindToParent(IIdentity parent)
        {
            Preconditions.CheckNotNull(parent);

            if (this.KnownParent.Exists(prev => !string.Equals(prev.Id, parent.Id)))
            {
                // this should not happen
                Events.ParentChanged(this.KnownParent.Map(prev => prev.Id).GetOrElse(string.Empty), parent.Id);
            }

            this.KnownParent = Option.Some(parent);
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.NestingInfo;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ConnectionInfo>();

            enum EventIds
            {
                ParentChanged = IdStart,
            }

            public static void ParentChanged(string previousParent, string currentParent) => Log.LogWarning((int)EventIds.ParentChanged, $"Swaping child device/module from parent {previousParent} to {currentParent}");
        }
    }
}
