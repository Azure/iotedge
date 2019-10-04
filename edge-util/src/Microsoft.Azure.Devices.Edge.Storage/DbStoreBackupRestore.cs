// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
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
            try
            {
                // This is a hack, make it better by not having to create another in-memory collection of items
                // to be backed up.
                IList<Item> items = new List<Item>();
                await dbStore.IterateBatch(
                int.MaxValue,
                (key, value) =>
                {
                    items.Add(new Item(key, value));
                    return Task.CompletedTask;
                });

                await this.backupRestore.BackupAsync(entityName, backupPath, items);
            }
            catch (IOException exception)
            {
                throw new IOException($"The backup operation for {entityName} failed with error.", exception);
            }
        }

        public async Task RestoreAsync(string entityName, IDbStore dbStore, string backupPath)
        {
            try
            {
                IList<Item> items = await this.backupRestore.RestoreAsync<IList<Item>>(entityName, backupPath);
                foreach (Item item in items)
                {
                    await dbStore.Put(item.Key, item.Value);
                }
            }
            catch (IOException exception)
            {
                throw new IOException($"The restore operation for {entityName} failed with error.", exception);
            }
        }
    }
}
