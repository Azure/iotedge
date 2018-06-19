// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System.Threading.Tasks;

    public interface IEncryptionProvider
    {
        Task<string> DecryptAsync(string encryptedText);

        Task<string> EncryptAsync(string plainText);
    }
}
