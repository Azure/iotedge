// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.IO;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ByteBufferConverter : IByteBufferConverter
    {
        readonly IByteBufferAllocator allocator;

        public ByteBufferConverter(IByteBufferAllocator allocator)
        {
            this.allocator = allocator;
        }

        public byte[] ToByteArray(IByteBuffer byteBuffer)
        {
            Preconditions.CheckNotNull(byteBuffer, nameof(byteBuffer));

            if (!byteBuffer.IsReadable())
            {
                return new byte[0];
            }

            int readableBytes = byteBuffer.ReadableBytes;
            using (var stream = new ReadOnlyByteBufferStream(byteBuffer, false))
            {
                using (var memoryStream = new MemoryStream())
                {
                    var buffer = new byte[readableBytes + 1];
                    int read = stream.Read(buffer, 0, buffer.Length);
                    memoryStream.Write(buffer, 0, read);
                    byte[] bytes = memoryStream.ToArray();
                    return bytes;
                }
            }
        }

        public IByteBuffer ToByteBuffer(byte[] bytes)
        {
            Preconditions.CheckNotNull(bytes, nameof(bytes));
            int length = bytes.Length;
            IByteBuffer byteBuffer = this.allocator.Buffer(length, length);
            byteBuffer.WriteBytes(bytes);
            return byteBuffer;
        }
    }
}
