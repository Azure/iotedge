// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Threading.Tasks;

    public interface IDbStore : IKeyValueStore<byte[], byte[]>
    {
        Task BackupAsync(string backupPath);
    }
}
