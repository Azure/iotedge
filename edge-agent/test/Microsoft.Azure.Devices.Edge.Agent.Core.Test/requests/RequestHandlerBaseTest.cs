// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Requests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class RequestHandlerBaseTest
    {
        [Theory]
        [InlineData("", typeof(ArgumentException))]
        [InlineData(null, typeof(ArgumentException))]
        [InlineData("{\"prop1\":\"foo\",\"prop2\":100}", null)]
        public async Task GetResponseTest(string payloadJson, Type expectedException)
        {
            // Arrange
            IRequestHandler requestHandler = new RequestHandlerImpl();

            // Act / Assert
            if (expectedException != null)
            {
                await Assert.ThrowsAsync(expectedException, () => requestHandler.HandleRequest(payloadJson));
            }
            else
            {
                string responsePayload = await requestHandler.HandleRequest(payloadJson);
                Assert.Equal(payloadJson, responsePayload);
            }
        }

        class RequestHandlerImpl : RequestHandlerBase<RequestPayload, RequestResponse>
        {
            protected override Task<RequestResponse> HandleRequestInternal(RequestPayload payload) =>
                Task.FromResult(new RequestResponse(payload.Prop1, payload.Prop2));
        }

        class RequestPayload
        {
            [JsonConstructor]
            public RequestPayload(string prop1, int prop2)
            {
                this.Prop1 = prop1;
                this.Prop2 = prop2;
            }

            [JsonProperty("prop1")]
            public string Prop1 { get; }

            [JsonProperty("prop2")]
            public int Prop2 { get; }
        }

        class RequestResponse
        {
            [JsonConstructor]
            public RequestResponse(string prop1, int prop2)
            {
                this.Prop1 = prop1;
                this.Prop2 = prop2;
            }

            [JsonProperty("prop1")]
            public string Prop1 { get; }

            [JsonProperty("prop2")]
            public int Prop2 { get; }
        }
    }
}
