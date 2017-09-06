// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class SerDeExtensionsTest
    {
        [Fact]
        public void ToJsonTest()
        {
            Assert.Equal("", SerDeExtensions.ToJson(null));

            string str = "Foo Bar";
            Assert.Equal(str, SerDeExtensions.ToJson(str));

            ITestInterface testObj = new TestClass("Foo") { Prop2 = 100 };
            string json = SerDeExtensions.ToJson(testObj);
            string expectedJson = @"{""$type"":""Microsoft.Azure.Devices.Edge.Storage.Test.SerDeExtensionsTest+TestClass, Microsoft.Azure.Devices.Edge.Storage.Test"",""Prop1"":""Foo"",""Prop2"":100}";
            Assert.Equal(expectedJson, json);

            ITestInterface jsonConvertedObj = SerDeExtensions.FromJson<ITestInterface>(json);
            Assert.NotNull(jsonConvertedObj);
            Assert.Equal("Foo", jsonConvertedObj.GetProp1());
            Assert.Equal(100, jsonConvertedObj.GetProp2());
        }

        [Fact]
        public void FromJsonTest()
        {
            Assert.Null(SerDeExtensions.FromJson<ITestInterface>(null));
            Assert.Null(SerDeExtensions.FromJson<ITestInterface>(""));

            Assert.Equal(0, SerDeExtensions.FromJson<int>(null));
            Assert.Equal(0, SerDeExtensions.FromJson<long>(""));

            string str = "Foo Bar";
            Assert.Equal(str, SerDeExtensions.FromJson<string>(str));
        }

        [Fact]
        public void StringToBytesRoundtripTest()
        {
            string testStr = "Test String Value";
            byte[] bytes = SerDeExtensions.ToBytes(testStr);
            string convertedStr = SerDeExtensions.FromBytes(bytes);
            Assert.Equal(testStr, convertedStr);
        }

        [Fact]
        public void StringToBytesTest()
        {
            byte[] bytes = SerDeExtensions.ToBytes((string)null);
            Assert.NotNull(bytes);
            Assert.Equal(0, bytes.Length);

            bytes = SerDeExtensions.ToBytes("  ");
            Assert.NotNull(bytes);
            Assert.Equal(0, bytes.Length);
        }

        [Fact]
        public void FromBytesTest()
        {
            Assert.Equal("", SerDeExtensions.FromBytes(null));
            Assert.Equal("", SerDeExtensions.FromBytes(new byte[0]));
        }

        [Fact]
        public void ObjectToBytesTest()
        {
            Assert.Null(SerDeExtensions.ToBytes((object)null));

            var testBytes = new byte[] { 1, 2, 123 };
            Assert.Equal(testBytes, SerDeExtensions.ToBytes(testBytes));
        }

        [Fact]
        public void ObjectFromBytesTest()
        {
            Assert.Null(SerDeExtensions.FromBytes<ITestInterface>(null));

            var testBytes = new byte[] { 1, 2, 123 };
            Assert.Equal(testBytes, SerDeExtensions.FromBytes<byte[]>(testBytes));
        }

        [Fact]
        public void ObjectToBytesRoundtripTest()
        {
            ITestInterface testObj = new TestClass("Foo") { Prop2 = 100 };
            var bytes = SerDeExtensions.ToBytes(testObj);
            ITestInterface testObj2 = SerDeExtensions.FromBytes<ITestInterface>(bytes);
            Assert.NotNull(testObj2);
            Assert.Equal(testObj.GetProp1(), testObj2.GetProp1());
            Assert.Equal(testObj.GetProp2(), testObj2.GetProp2());
        }

        public interface ITestInterface
        {
            string GetProp1();
            long GetProp2();
        }

        class TestClass : ITestInterface
        {
            public string Prop1 { get; }
            public long Prop2 { get; set; }

            public string GetProp1() => this.Prop1;

            public long GetProp2() => this.Prop2;

            public TestClass()
                : this(null)
            {
            }

            [JsonConstructor]
            public TestClass(string prop1)
            {
                this.Prop1 = prop1;
            }
        }
    }
}
