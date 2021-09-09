// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Threading.Tasks;

    public interface ISinkFactory<T>
    {
        Task<ISink<T>> CreateAsync(string hubName);
    }
}