// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Threading.Tasks;

    public interface IDbStoreProviderBackupRestore : IDbStore
    {
        Task BackupAsync(string backupPath, IDbStoreProvider dbStoreProvider);

        Task RestoreAsync(string backupPath);
    }
}
