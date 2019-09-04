// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;

    public class DeviceProvisioningFixture : BaseFixture
    {
        protected readonly IEdgeDaemon daemon;
        protected Option<EdgeDevice> device;

        public DeviceProvisioningFixture()
        {
            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath);
            this.device = Option.None<EdgeDevice>();
        }
    }
}
