// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System.Threading.Tasks;

    public class NullEncryptionProvider : IEncryptionProvider
    {
        public static NullEncryptionProvider Instance => new NullEncryptionProvider();

        public Task<string> DecryptAsync(string encryptedText) => Task.FromResult(encryptedText);

        public Task<string> EncryptAsync(string plainText) => Task.FromResult(plainText);
    }
}
