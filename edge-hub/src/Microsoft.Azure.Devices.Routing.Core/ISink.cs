// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISink<T>
    {
        Task<ISinkResult<T>> ProcessAsync(T t, CancellationToken token);

        Task<ISinkResult<T>> ProcessAsync(ICollection<T> ts, CancellationToken token);

        Task CloseAsync(CancellationToken token);
    }
}
