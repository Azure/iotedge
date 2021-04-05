// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NotNested : INestingInfo
    {
        public static INestingInfo Instance { get; } = new NotNested();

        public Option<IIdentity> KnownParent => Option.None<IIdentity>();
        public bool IsDirectClient => true;

        public void BindToParent(IIdentity parent) => throw new InvalidOperationException("Nesting info is not supported");
    }
}
