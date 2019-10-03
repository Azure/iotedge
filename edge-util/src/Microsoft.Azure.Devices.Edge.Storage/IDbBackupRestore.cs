// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Threading.Tasks;

    public interface IDbBackupRestore : IDbStore
    {
        Task BackupAsync(string backupPath);

        Task RestoreAsync(string backupPath);
    }
}
