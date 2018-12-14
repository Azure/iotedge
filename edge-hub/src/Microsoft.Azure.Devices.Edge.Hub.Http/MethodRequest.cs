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
        const int DeviceMethodDefaultConnectTimeoutInSeconds = 0;
        byte[] payloadBytes;
        
        public MethodRequest(string methodName, JRaw payload)
            : this(methodName, payload, DeviceMethodDefaultResponseTimeoutInSeconds, DeviceMethodDefaultConnectTimeoutInSeconds)
        {
        }

        [JsonConstructor]
        public MethodRequest(string methodName, JRaw payload, int? responseTimeoutInSeconds, int? connectTimeoutInSeconds)
        {
            this.MethodName = methodName;
            this.Payload = payload;
            this.ResponseTimeoutInSeconds = responseTimeoutInSeconds ?? DeviceMethodDefaultResponseTimeoutInSeconds;
            this.ConnectTimeoutInSeconds = connectTimeoutInSeconds ?? DeviceMethodDefaultConnectTimeoutInSeconds;
        }

        [JsonProperty("methodName", Required = Required.Always)]
        public string MethodName { get; }

        [JsonProperty("payload")]
        public JRaw Payload { get; }

        [JsonProperty("responseTimeoutInSeconds")]
        internal int ResponseTimeoutInSeconds { get; }

        [JsonProperty("connectTimeoutInSeconds")]
        internal int ConnectTimeoutInSeconds { get; }

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
