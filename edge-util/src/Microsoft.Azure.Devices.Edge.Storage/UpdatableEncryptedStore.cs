// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class UpdatableEncryptedStore<TK, TV> : EncryptedStore<TK, TV>
    {
        public UpdatableEncryptedStore(IKeyValueStore<TK, string> entityStore, IEncryptionProvider encryptionProvider)
            : base(entityStore, encryptionProvider)
        {
        }

        protected override async Task<string> EncryptValue(string value)
        {
            string encryptedValue = await this.EncryptionProvider.EncryptAsync(value);
            var encryptedData = new EncryptedData(true, encryptedValue);
            string encryptedDataString = encryptedData.ToJson();
            return encryptedDataString;
        }

        protected override async Task<string> DecryptValue(string value)
        {
            string decryptedValue;
            try
            {
                var encryptedData = value.FromJson<EncryptedData>();
                if (encryptedData.Encrypted)
                {
                    decryptedValue = await this.EncryptionProvider.DecryptAsync(encryptedData.Payload);
                }
                else
                {
                    decryptedValue = encryptedData.Payload;
                }
            }
            catch (JsonSerializationException)
            {
                decryptedValue = value;
            }

            return decryptedValue;
        }

        public class EncryptedData
        {
            public EncryptedData(bool encrypted, string payload)
            {
                this.Encrypted = encrypted;
                this.Payload = payload;
            }

            [JsonProperty("encrypted", Required = Required.Always)]
            public bool Encrypted { get; }

            [JsonProperty("payload", Required = Required.Always)]
            public string Payload { get; }
        }
    }
}
