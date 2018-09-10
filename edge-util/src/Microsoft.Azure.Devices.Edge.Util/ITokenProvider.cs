// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Threading.Tasks;

    public interface ITokenProvider
    {
        Task<string> GetTokenAsync(Option<TimeSpan> ttl);
    }
}
