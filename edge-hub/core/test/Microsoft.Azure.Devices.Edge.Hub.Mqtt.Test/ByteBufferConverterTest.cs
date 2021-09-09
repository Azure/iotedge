// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class ByteBufferConverterTest
    {
        public static IEnumerable<object[]> GetInvalidTestByteArrays()
        {
            yield return new object[] { null, typeof(ArgumentNullException) };
        }

        public static IEnumerable<object[]> GetInvalidTestByteBuffer()
        {
            yield return new object[] { null, typeof(ArgumentNullException) };
        }

        public static IEnumerable<object[]> GetTestByteArrays()
        {
            var rand = new Random();

            var bytes = new byte[100];
            rand.NextBytes(bytes);
            yield return new object[] { bytes };

            bytes = new byte[200];
            rand.NextBytes(bytes);
            yield return new object[] { bytes };

            bytes = new byte[20];
            rand.NextBytes(bytes);
            yield return new object[] { bytes };

            bytes = new byte[64];
            rand.NextBytes(bytes);
            yield return new object[] { bytes };
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetTestByteArrays))]
        public void ByteBufferRoundtripTest_Pooled(byte[] input)
        {
            IByteBufferConverter converter = new ByteBufferConverter(PooledByteBufferAllocator.Default);
            IByteBuffer byteBuffer = converter.ToByteBuffer(input);
            Assert.NotNull(byteBuffer);
            byte[] convertedBytes = converter.ToByteArray(byteBuffer);
            Assert.NotNull(convertedBytes);
            Assert.Equal(input, convertedBytes);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidTestByteArrays))]
        public void ByteArrayConversionErrorTest_Pooled(byte[] input, Type expectedException)
        {
            IByteBufferConverter converter = new ByteBufferConverter(PooledByteBufferAllocator.Default);
            Assert.Throws(expectedException, () => converter.ToByteBuffer(input));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidTestByteBuffer))]
        public void ByteBufferConversionErrorTest_Pooled(IByteBuffer input, Type expectedException)
        {
            IByteBufferConverter converter = new ByteBufferConverter(PooledByteBufferAllocator.Default);
            Assert.Throws(expectedException, () => converter.ToByteArray(input));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetTestByteArrays))]
        public void ByteBufferRoundtripTest_Unpooled(byte[] input)
        {
            IByteBufferConverter converter = new ByteBufferConverter(UnpooledByteBufferAllocator.Default);
            IByteBuffer byteBuffer = converter.ToByteBuffer(input);
            Assert.NotNull(byteBuffer);
            byte[] convertedBytes = converter.ToByteArray(byteBuffer);
            Assert.NotNull(convertedBytes);
            Assert.Equal(input, convertedBytes);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidTestByteArrays))]
        public void ByteArrayConversionErrorTest_Unpooled(byte[] input, Type expectedException)
        {
            IByteBufferConverter converter = new ByteBufferConverter(UnpooledByteBufferAllocator.Default);
            Assert.Throws(expectedException, () => converter.ToByteBuffer(input));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidTestByteBuffer))]
        public void ByteBufferConversionErrorTest_Unpooled(IByteBuffer input, Type expectedException)
        {
            IByteBufferConverter converter = new ByteBufferConverter(UnpooledByteBufferAllocator.Default);
            Assert.Throws(expectedException, () => converter.ToByteArray(input));
        }
    }
}
