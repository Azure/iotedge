// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper
{
    using System.Threading.Tasks;

    public interface ISignatureProvider
    {
        Task<string> SignAsync(string keyName, string data);
    }
}
