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
        // Using null because BindToParent is implemented with interlocked operations and that does not work with value types (like Option)
        IIdentity knownParent = null;

        public Option<IIdentity> KnownParent
        {
            get
            {
                var result = this.knownParent;
                return result == null ? Option.None<IIdentity>() : Option.Some(result);
            }
        }

        public ConnectionInfo(bool isDirectClient)
        {
            this.IsDirectClient = isDirectClient;
        }

        public bool IsDirectClient { get; }

        public void BindToParent(IIdentity parent)
        {
            Preconditions.CheckNotNull(parent);

            var previousParent = Interlocked.Exchange(ref this.knownParent, parent);
            if (previousParent != null && !string.Equals(previousParent.Id, parent.Id))
            {
                // this should not happen
                Events.ParentChanged(previousParent.Id, parent.Id);
            }
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
