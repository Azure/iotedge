// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
