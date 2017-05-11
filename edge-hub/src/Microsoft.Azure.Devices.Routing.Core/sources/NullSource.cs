// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Sources
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class NullSource : Source
    {
        public NullSource(Router router)
            : base(router)
        {
        }

        public override Task RunAsync() => TaskEx.Done;
    }
}