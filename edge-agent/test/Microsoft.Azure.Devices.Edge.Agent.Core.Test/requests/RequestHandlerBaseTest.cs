// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Requests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Util;
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
        [InlineData("{\"prop1\":\"1d\",\"prop2\":100}", null)]
        [InlineData("{\"prop1\":\"yyyy-MM-ddTHH:mm:ssZ\",\"prop2\":100}", null)] // RFC3339 format
        [InlineData("{\"prop1\":\"999999999\",\"prop2\":100}", null)] // Unix Timestamp
        public async Task GetResponseTest(string payloadJson, Type expectedException)
        {
            // Arrange
            IRequestHandler requestHandler = new RequestHandlerImpl();

            // Act / Assert
            if (expectedException != null)
            {
                await Assert.ThrowsAsync(expectedException, () => requestHandler.HandleRequest(Option.Maybe(payloadJson), CancellationToken.None));
            }
            else
            {
                Option<string> responsePayload = await requestHandler.HandleRequest(Option.Maybe(payloadJson), CancellationToken.None);
                Assert.True(responsePayload.HasValue);
                Assert.Equal(payloadJson, responsePayload.OrDefault());
            }
        }

        class RequestHandlerImpl : RequestHandlerBase<RequestPayload, RequestResponse>
        {
            public override string RequestName => "TestImpl";

            protected override Task<Option<RequestResponse>> HandleRequestInternal(Option<RequestPayload> payload, CancellationToken token)
            {
                RequestPayload requestPayload = payload.Expect(() => new ArgumentException("Payload should not be null"));
                return Task.FromResult(Option.Some(new RequestResponse(requestPayload.Prop1, requestPayload.Prop2)));
            }
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
