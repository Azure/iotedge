// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using Microsoft.Azure.Devices.Edge.Util;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class DbStoreBackupRestore : IDbStoreBackupRestore
    {
        readonly IBackupRestore backupRestore;

        public DbStoreBackupRestore(IBackupRestore backupRestore)
        {
            this.backupRestore = backupRestore;
        }

        public async Task BackupAsync(string entityName, IDbStore dbStore, string backupPath)
        {
            Preconditions.CheckNonWhiteSpace(entityName, nameof(entityName));
            Preconditions.CheckNotNull(dbStore, nameof(dbStore));
            Preconditions.CheckNonWhiteSpace(backupPath, nameof(backupPath));

            try
            {
                IList<Item> items = new List<Item>();
                await dbStore.IterateBatch(
                    int.MaxValue,
                    (key, value) =>
                    {
                        items.Add(new Item(key, value));
                        return Task.CompletedTask;
                    }
                );

                await this.backupRestore.BackupAsync(entityName, backupPath, items);
            }
            catch (IOException exception)
            {
                throw new IOException($"The backup operation for {entityName} failed with error.", exception);
            }
        }

        public async Task RestoreAsync(string entityName, IDbStore dbStore, string backupPath)
        {
            Preconditions.CheckNonWhiteSpace(entityName, nameof(entityName));
            Preconditions.CheckNotNull(dbStore, nameof(dbStore));
            Preconditions.CheckNonWhiteSpace(backupPath, nameof(backupPath));

            try
            {
                IList<Item> items = await this.backupRestore.RestoreAsync<IList<Item>>(entityName, backupPath);

                if (items != null)
                {
                    foreach (Item item in items)
                    {
                        await dbStore.Put(item.Key, item.Value);
                    }
                }
            }
            catch (IOException exception)
            {
                throw new IOException($"The restore operation for {entityName} failed with error.", exception);
            }
        }
    }
}
