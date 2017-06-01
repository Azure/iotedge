// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.IO;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class ByteBufferHelper
    {
        public static byte[] ToByteArray(this IByteBuffer byteBuffer)
        {
            Preconditions.CheckNotNull(byteBuffer, nameof(byteBuffer));

            if (!byteBuffer.IsReadable())
            {
                return new byte[0];
            }

            using (var stream = new ReadOnlyByteBufferStream(byteBuffer, false))
            {
                var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                byte[] bytes = memoryStream.ToArray();
                return bytes;
            }
        }

        public static IByteBuffer ToByteBuffer(this byte[] bytes)
        {
            Preconditions.CheckNotNull(bytes, nameof(bytes));
            PooledByteBufferAllocator allocator = PooledByteBufferAllocator.Default;
            int length = bytes.Length;
            IByteBuffer byteBuffer = allocator.Buffer(length, length);
            byteBuffer.WriteBytes(bytes);
            return byteBuffer;
        }
    }
}