// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public static class DiskFile
    {
        static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(60);

        public static Task<string> ReadAllAsync(string path)
            => ReadAllAsync(path, DefaultOperationTimeout);

        public static async Task<string> ReadAllAsync(string path, TimeSpan timeout)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));
            using (var reader = new StreamReader(File.OpenRead(path)))
            {
                return await reader.ReadToEndAsync().TimeoutAfter(timeout);
            }
        }

        public static Task WriteAllAsync(string path, string content)
            => WriteAllAsync(path, content, DefaultOperationTimeout);

        public static Task WriteAllAsync(string path, string content, TimeSpan timeout)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));
            Preconditions.CheckNonWhiteSpace(content, nameof(content));

            async Task WriteOperation()
            {
                using (var writer = new StreamWriter(File.Open(path, FileMode.Create)))
                {
                    await writer.WriteAsync(content);
                    await writer.FlushAsync();
                }
            }

            return WriteOperation().TimeoutAfter(timeout);
        }
    }
}
