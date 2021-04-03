// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface INestingInfo
    {
        void BindToParent(IIdentity parent);

        Option<IIdentity> KnownParent { get; }
        bool IsDirectClient { get; }
    }
}
