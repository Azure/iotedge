// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Azure.Devices.Edge.Util;
    using Nito.AsyncEx;
    using ProtoBuf;

    /// <summary>
    /// Provides an in memory implementation of the IDbStore with backup and restore functionality.
    /// </summary>
    class InMemoryDbStoreBackupRestore : DbStoreWithBackupRestore
    {
        readonly string entityName;

        InMemoryDbStoreBackupRestore(string entityName)
            : base()
        {
            Preconditions.CheckNonWhiteSpace(entityName, nameof(entityName));
            this.entityName = entityName;
        }

        public static async Task<InMemoryDbStoreWithBackupRestore> CreateAsync(string entityName, string backupPath)
        {
            Preconditions.CheckNonWhiteSpace(backupPath, nameof(backupPath));
            InMemoryDbStoreBackupRestore store = new InMemoryDbStoreBackupRestore(entityName);
            await store.RestoreAsync(backupPath);
            return store;
        }

        public async Task RestoreAsync(string backupPath)
        {
            string backupFileName = HttpUtility.UrlEncode(this.entityName);
            string entityBackupPath = Path.Combine(backupPath, $"{backupFileName}.bin");
            if (!File.Exists(entityBackupPath))
            {
                throw new IOException($"The backup data for {this.entityName} doesn't exist.");
            }

            try
            {
                using (FileStream file = File.OpenRead(entityBackupPath))
                {
                    IList<Item> backedUpItems = Serializer.Deserialize<IList<Item>>(file);
                    foreach (Item item in backedUpItems)
                    {
                        this.keyValues.Add(item);
                    }
                }
            }
            catch (IOException exception)
            {
                throw new IOException($"The restore operation for {this.entityName} failed with error.", exception);
            }
        }

        public async Task BackupAsync(string backupPath)
        {
            string backupFileName = HttpUtility.UrlEncode(this.entityName);
            string newBackupPath = Path.Combine(backupPath, $"{backupFileName}.bin");
            try
            {
                using (FileStream file = File.Create(newBackupPath))
                {
                    using (await this.listLock.WriterLockAsync(CancellationToken.None))
                    {
                        Serializer.Serialize(file, this.keyValues.AllItems);
                    }
                }
            }
            catch (IOException exception)
            {
                // Delete the backup data if anything was created as it will likely be corrupt.
                if (File.Exists(newBackupPath))
                {
                    File.Delete(newBackupPath);
                }

                throw new IOException($"The backup operation for {this.entityName} failed with error.", exception);
            }
        }
    }
}
