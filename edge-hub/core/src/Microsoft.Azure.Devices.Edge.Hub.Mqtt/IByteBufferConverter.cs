// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using DotNetty.Buffers;

    public interface IByteBufferConverter
    {
        byte[] ToByteArray(IByteBuffer byteBuffer);

        IByteBuffer ToByteBuffer(byte[] bytes);
    }
}
