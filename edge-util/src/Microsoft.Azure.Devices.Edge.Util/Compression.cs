// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;

    public static class Compression
    {
        public static byte[] CompressToGzip(byte[] input)
        {
            using (var compressedStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    gzipStream.Write(input, 0, input.Length);
                }

                var compressedBytes = compressedStream.ToArray();
                return compressedBytes;
            }
        }

        public static byte[] DecompressFromGzip(byte[] input)
        {
            using (var decompressedStream = new MemoryStream())
            {
                using (var compressedStream = new MemoryStream(input))
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    gzipStream.CopyTo(decompressedStream);
                }

                var decompressedBytes = decompressedStream.ToArray();
                return decompressedBytes;
            }
        }
    }
}
