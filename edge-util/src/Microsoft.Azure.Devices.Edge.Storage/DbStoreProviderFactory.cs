// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Threading.Tasks;

    /// <summary>
    /// Factory with methods to obtain instances of DB Store Providers.
    /// </summary>
    public static class DbStoreProviderFactory
    {
        public static IDbStoreProvider GetInMemoryDbStore()
        {
            return new InMemoryDbStoreProvider();
        }

        public static async Task<IDbStoreProvider> WithBackupRestore(
            this IDbStoreProvider dbStoreProvider,
            string backupPath,
            IDataBackupRestore dataBackupRestore)
        {
            return await DbStoreProviderWithBackupRestore.CreateAsync(dbStoreProvider, backupPath, dataBackupRestore);
        }
    }
}
