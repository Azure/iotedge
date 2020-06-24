// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Sources
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NullSource : Source
    {
        public NullSource(Router router)
            : base(router)
        {
        }

        public override Task RunAsync() => TaskEx.Done;
    }
}
