
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.IO;
    using System.Threading.Tasks;

    public class DbStoreBackupRestore : IDbStoreBackupRestore
    {
        public async Task BackupAsync(string entityName, IDbStore dbStore, string backupPath)
        {
            IItemKeyedCollectionBackupRestore itemKeyedCollectionBackupRestore = new ItemKeyedCollectionBackupRestore(backupPath);
            try
            {
                // This is a hack, make it better by not having to create another in-memory collection of items
                // to be backed up.
                ItemKeyedCollection items = new ItemKeyedCollection(new ByteArrayComparer());
                await dbStore.IterateBatch(
                int.MaxValue,
                (key, value) =>
                {
                    items.Add(new Item(key, value));
                    return Task.CompletedTask;
                });

                await itemKeyedCollectionBackupRestore.BackupAsync(entityName, items);
            }
            catch (IOException exception)
            {
                throw new IOException($"The backup operation for {entityName} failed with error.", exception);
            }
        }

        public async Task RestoreAsync(string entityName, IDbStore dbStore, string backupPath)
        {
            IItemKeyedCollectionBackupRestore itemKeyedCollectionBackupRestore = new ItemKeyedCollectionBackupRestore(backupPath);
            try
            {
                ItemKeyedCollection items = await itemKeyedCollectionBackupRestore.RestoreAsync(entityName);
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
