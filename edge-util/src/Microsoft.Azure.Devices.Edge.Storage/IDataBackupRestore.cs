// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Threading.Tasks;

    public interface IDataBackupRestore
    {
        Task BackupAsync<T>(string name, string backupPath, T data);

        Task<T> RestoreAsync<T>(string name, string backupPath);

        SerializationFormat DataBackupFormat { get; }
    }
}
