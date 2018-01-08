// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Edge.SimulateEdgeDevice.Test
{
    using System;
    using Xunit.Sdk;

    [TraitDiscoverer("Microsoft.Azure.Devices.Edge.SimulateEdgeDevice.Test.DeployDiscoverer", "Microsoft.Azure.Devices.Edge.SimulateEdgeDevice.Test")]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class DeployAttribute : Attribute, ITraitAttribute
    {
    }
}
