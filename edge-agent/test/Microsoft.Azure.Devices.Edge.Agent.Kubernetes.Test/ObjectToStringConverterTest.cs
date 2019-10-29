// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class ObjectToStringConverterTest
    {
        [Fact]
        public void SerializesWholeObjectInsideWrapperAsString()
        {
            var wrapper = new Wrapper { Inner = new ObjectToSerialize { Name = "some name here" } };

            string json = JsonConvert.SerializeObject(wrapper);

            Assert.Equal("{\"Inner\":\"{\\\"Name\\\":\\\"some name here\\\"}\"}", json);
        }

        [Fact]
        public void DeserializesWholeObjectInsideWrapperFromString()
        {
            string json = "{\"Inner\":\"{\\\"Name\\\":\\\"some name here\\\"}\"}";

            Wrapper wrapper = JsonConvert.DeserializeObject<Wrapper>(json);

            Assert.Equal("some name here", wrapper.Inner.Name);
        }

        class ObjectToSerialize
        {
            public string Name { get; set; }
        }

        class Wrapper
        {
            [JsonConverter(typeof(ObjectToStringConverter<ObjectToSerialize>))]
            public ObjectToSerialize Inner { get; set; }
        }
    }
}
