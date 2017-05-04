// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class ByteBufferHelperTest
    {
        static IEnumerable<object[]> GetInvalidTestByteArrays()
        {
            yield return new object[] { null, typeof(ArgumentNullException) };            
        }

        static IEnumerable<object[]> GetInvalidTestByteBuffer()
        {
            yield return new object[] { null, typeof(ArgumentNullException) };
        }

        static IEnumerable<object[]> GetTestByteArrays()
        {
            var rand = new Random();
            var bytes = new byte[100];
            rand.NextBytes(bytes);
            yield return new object[] { bytes };
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetTestByteArrays))]
        public void ByteBufferRoundtripTest(byte[] input)
        {
            IByteBuffer byteBuffer = input.ToByteBuffer();
            Assert.NotNull(byteBuffer);
            byte[] convertedBytes = byteBuffer.ToByteArray();
            Assert.NotNull(convertedBytes);
            Assert.Equal(input, convertedBytes);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidTestByteArrays))]
        public void ByteArrayConversionErrorTest(byte[] input, Type expectedException)
        {
            Assert.Throws(expectedException, () => input.ToByteBuffer());            
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidTestByteBuffer))]
        public void ByteBufferConversionErrorTest(IByteBuffer input, Type expectedException)
        {
            Assert.Throws(expectedException, () => input.ToByteArray());
        }
    }
}