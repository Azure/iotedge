// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.Text;

    public class MethodRequest
    {
        const int DeviceMethodDefaultResponseTimeoutInSeconds = 30;

        byte[] payloadBytes;

        public MethodRequest()
        {
            this.ResponseTimeoutInSeconds = DeviceMethodDefaultResponseTimeoutInSeconds;
        }

        [JsonProperty("methodName", Required = Required.Always)]
        public string MethodName { get; set; }

        [JsonProperty("payload")]
        public JRaw Payload { get; set; }

        [JsonProperty("responseTimeoutInSeconds")]
        internal int ResponseTimeoutInSeconds { get; set; }

        [JsonProperty("connectTimeoutInSeconds")]
        internal int ConnectTimeoutInSeconds { get; set; }

        [JsonIgnore]
        public TimeSpan ResponseTimeout => TimeSpan.FromSeconds(this.ResponseTimeoutInSeconds);

        [JsonIgnore]
        public TimeSpan ConnectTimeout => TimeSpan.FromSeconds(this.ConnectTimeoutInSeconds);

        [JsonIgnore]
        public byte[] PayloadBytes
        {
            get
            {
                if (this.payloadBytes == null && this.Payload != null)
                {
                    // TODO - Should we allow only UTF8 or other encodings as well? Need to check what IoTHub does.
                    this.payloadBytes = Encoding.UTF8.GetBytes((string)this.Payload);
                }
                return this.payloadBytes;
            }
        }
    }
}
