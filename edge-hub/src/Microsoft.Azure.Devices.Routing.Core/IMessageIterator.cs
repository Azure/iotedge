// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMessageIterator
    {
        Task<IEnumerable<IMessage>> GetNext(int batchSize);
    }
}
