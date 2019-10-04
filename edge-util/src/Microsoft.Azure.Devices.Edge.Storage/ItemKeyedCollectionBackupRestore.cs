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
    class ItemKeyedCollectionBackupRestore : IItemKeyedCollectionBackupRestore
    {
        readonly string backupPath;

        public ItemKeyedCollectionBackupRestore(string backupPath)
        {
            Preconditions.CheckNonWhiteSpace(backupPath, nameof(backupPath));
            this.backupPath = backupPath;
        }

        public Task<ItemKeyedCollection> RestoreAsync(string name)
        {
            string backupFileName = HttpUtility.UrlEncode(name);
            string entityBackupPath = Path.Combine(backupPath, $"{backupFileName}.bin");
            if (!File.Exists(entityBackupPath))
            {
                throw new IOException($"The backup data at {this.backupPath} doesn't exist.");
            }

            ItemKeyedCollection itemKeyedCollection = new ItemKeyedCollection(new ByteArrayComparer());
            try
            {
                using (FileStream file = File.OpenRead(entityBackupPath))
                {
                    IList<Item> backedUpItems = Serializer.Deserialize<IList<Item>>(file);
                    foreach (Item item in backedUpItems)
                    {
                        itemKeyedCollection.Add(item);
                    }
                }
            }
            catch (IOException exception)
            {
                //throw new IOException($"The restore operation for {this.entityName} failed with error.", exception);
                throw new IOException($"The restore operation failed with error.", exception);
            }

            return Task.FromResult(itemKeyedCollection);
        }

        public Task BackupAsync(string name, ItemKeyedCollection itemKeyedCollection)
        {
            string backupFileName = HttpUtility.UrlEncode(name);
            string newBackupPath = Path.Combine(this.backupPath, $"{backupFileName}.bin");

            try
            {
                using (FileStream file = File.Create(newBackupPath))
                {
                    Serializer.Serialize(file, itemKeyedCollection.AllItems);
                }
            }
            catch (IOException exception)
            {
                // Delete the backup data if anything was created as it will likely be corrupt.
                if (File.Exists(newBackupPath))
                {
                    File.Delete(newBackupPath);
                }

                //throw new IOException($"The backup operation for {this.entityName} failed with error.", exception);
                throw new IOException($"The backup operation failed with error.", exception);
            }

            return Task.CompletedTask;
        }
    }
}
