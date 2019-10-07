// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Azure.Devices.Edge.Util;
    using ProtoBuf;

    /// <summary>
    /// Provides an in memory implementation of the IDbStore with backup and restore functionality.
    /// </summary>
    public class ProtoBufBackupRestore : IBackupRestore
    {
        public ProtoBufBackupRestore()
        {
        }

        public Task<T> RestoreAsync<T>(string name, string backupPath)
        {
            Preconditions.CheckNonWhiteSpace(name, nameof(name));
            Preconditions.CheckNonWhiteSpace(backupPath, nameof(backupPath));

            string backupFileName = HttpUtility.UrlEncode(name);
            string entityBackupPath = Path.Combine(backupPath, $"{backupFileName}.bin");
            if (!File.Exists(entityBackupPath))
            {
                throw new IOException($"The backup data at {backupPath} doesn't exist.");
            }

            try
            {
                using (FileStream file = File.OpenRead(entityBackupPath))
                {
                    return Task.FromResult(Serializer.Deserialize<T>(file));
                }
            }
            catch (Exception exception) when (
            exception is IOException
            || exception is ProtoException)
            {
                //throw new IOException($"The restore operation for {this.entityName} failed with error.", exception);
                throw new IOException($"The restore operation failed with error.", exception);
            }
        }

        public Task BackupAsync<T>(string name, string backupPath, T data)
        {
            Preconditions.CheckNonWhiteSpace(name, nameof(name));
            Preconditions.CheckNonWhiteSpace(backupPath, nameof(backupPath));

            string backupFileName = HttpUtility.UrlEncode(name);
            string newBackupPath = Path.Combine(backupPath, $"{backupFileName}.bin");

            try
            {
                using (FileStream file = File.Create(newBackupPath))
                {
                    Serializer.Serialize(file, data);
                }
            }
            catch (Exception exception) when (
            exception is IOException
            || exception is ProtoException)
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
