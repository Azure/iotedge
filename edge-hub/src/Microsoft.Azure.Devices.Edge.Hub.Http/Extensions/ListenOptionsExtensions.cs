// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Extensions
{
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Adapters;

    public static class ListenOptionsExtensions
    {
        public static ListenOptions UseHttpsExtensions(this ListenOptions options, HttpsConnectionAdapterOptions httpsOptions)
        {
            options.ConnectionAdapters.Add(new HttpsExtensionConnectionAdapter(httpsOptions));
            return options;
        }
    }
}
