// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System.Threading.Tasks;

    /// <summary>
    /// Signature provider.
    /// </summary>
    public interface ISignatureProvider
    {
        Task<string> SignAsync(string data);
    }
}
