// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Threading.Tasks;

    public interface IDbStoreBackupRestore
    {
        Task BackupAsync(string entityName, IDbStore dbStore, string backupPath);

        Task RestoreAsync(string entityName, IDbStore dbStore, string backupPath);
    }
}
