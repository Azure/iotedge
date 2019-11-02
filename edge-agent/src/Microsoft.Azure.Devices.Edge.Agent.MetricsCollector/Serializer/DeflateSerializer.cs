// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;

    /// <summary>
    /// Simple wrapper for the System.IO.Compression.DeflateStream class to make it work on byte arrays.
    /// </summary>
    public static class DeflateSerializer
    {
        public static byte[] Compress(IEnumerable<byte> bytes)
        {
            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (Stream compressionStream = new DeflateStream(compressedStream, CompressionLevel.Optimal, true))
                {
                    foreach (byte b in bytes)
                    {
                        compressionStream.WriteByte(b);
                    }
                }

                return compressedStream.ToArray();
            }
        }

        public static byte[] Decompress(IEnumerable<byte> bytes)
        {
            using (MemoryStream decompressedStream = new MemoryStream())
            {
                using (Stream decompressionStream = new DeflateStream(new IEnumerableReadStream(bytes), CompressionMode.Decompress, false))
                {
                    decompressionStream.CopyTo(decompressedStream);
                }

                return decompressedStream.ToArray();
            }
        }
    }

    class IEnumerableReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position { get; set; }

        IEnumerator<byte> source;

        public IEnumerableReadStream(IEnumerable<byte> source)
        {
            this.source = source.GetEnumerator();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int i = 0;
            while (i < count && this.source.MoveNext())
            {
                buffer[i++ + offset] = this.source.Current;
            }

            return i;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
