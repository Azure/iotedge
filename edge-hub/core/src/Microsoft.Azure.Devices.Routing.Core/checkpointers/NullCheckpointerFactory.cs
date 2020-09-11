// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    using System.Threading.Tasks;

    public class NullCheckpointerFactory : ICheckpointerFactory
    {
        public Task<ICheckpointer> CreateAsync(string id) => Task.FromResult(NullCheckpointer.Instance);
        public Task<ICheckpointer> CreateAsync(string id, string _, uint __) => Task.FromResult(NullCheckpointer.Instance);
    }
}
