// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Threading.Tasks;

    public interface IItemKeyedCollectionBackupRestore
    {
        Task BackupAsync(ItemKeyedCollection itemKeyedCollection);

        Task<ItemKeyedCollection> RestoreAsync();
    }
}
