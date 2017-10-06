// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.IO;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;

    public class DiskFile
    {
        public static async Task<string> ReadAllAsync(string path)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));
            using (var reader = new StreamReader(File.OpenRead(path)))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public static async Task WriteAllAsync(string path, string content)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));
            Preconditions.CheckNonWhiteSpace(content, nameof(content));

            using (var writer = new StreamWriter(File.Open(path, FileMode.Create)))
            {
                await writer.WriteAsync(content);
                await writer.FlushAsync();
            }
        }
    }
}