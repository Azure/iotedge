// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    public class EncryptionProvider : IEncryptionProvider
    {
        static readonly AsyncLock asyncLock = new AsyncLock();
        readonly string initializationVector;
        readonly WorkloadClient workloadClient;

        EncryptionProvider(Uri workloadUri, string apiVersion, string moduleId, string moduleGenerationid, string initializationVector)
        {
            this.initializationVector = initializationVector;
            this.workloadClient = new WorkloadClient(workloadUri, apiVersion, moduleId, moduleGenerationid);
        }

        public static async Task<EncryptionProvider> CreateAsync(string storagePath, Uri workloadUri, string edgeletWorkloadApiVersion, string moduleId, string genId, string initializationVectorFileName)
        {
            string ivFile = $"{storagePath}/{initializationVectorFileName}";
            string iv;
            using (await asyncLock.LockAsync())
            {
                if (!File.Exists(ivFile))
                {
                    iv = Guid.NewGuid().ToString("N");
                    await DiskFile.WriteAllAsync(ivFile, iv);
                }
                else
                {
                    iv = await DiskFile.ReadAllAsync(ivFile);
                }
            }

            return new EncryptionProvider(workloadUri, edgeletWorkloadApiVersion, moduleId, genId, iv);
        }

        public Task<string> DecryptAsync(string encryptedText) => this.workloadClient.DecryptAsync(this.initializationVector, encryptedText);

        public Task<string> EncryptAsync(string plainText) => this.workloadClient.EncryptAsync(this.initializationVector, plainText);
    }
}
