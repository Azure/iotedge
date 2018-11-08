// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    using System.Threading.Tasks;

    public class NullCheckpointerFactory : ICheckpointerFactory
    {
        public Task<ICheckpointer> CreateAsync(string id) => Task.FromResult(NullCheckpointer.Instance);
    }
}
