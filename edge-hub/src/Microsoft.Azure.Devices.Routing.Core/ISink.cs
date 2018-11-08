// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISink<T>
    {
        Task CloseAsync(CancellationToken token);

        Task<ISinkResult<T>> ProcessAsync(T t, CancellationToken token);

        Task<ISinkResult<T>> ProcessAsync(ICollection<T> ts, CancellationToken token);
    }
}
