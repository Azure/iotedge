// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Linq;
    using Newtonsoft.Json;

    public class AuthRequest
    {
        [JsonConstructor]
        AuthRequest(string version, string username, string password, string encodedCertificate, string[] encodedCertificateChain)
        {
            this.Version = version;
            this.Username = username;
            this.Password = password;
            this.EncodedCertificate = encodedCertificate;

            if (encodedCertificateChain != null)
            {
                this.EncodedCertificateChain = encodedCertificateChain.ToArray();
            }
        }

        [JsonProperty("version", Required = Required.Always)]
        public string Version { get; }

        [JsonProperty("username", Required = Required.Always)]
        public string Username { get; }

        [JsonProperty("password")]
        public string Password { get; }

        [JsonProperty("certificate")]
        public string EncodedCertificate { get; }

        [JsonProperty("certificateChain")]
        public string[] EncodedCertificateChain { get; }
    }
}
