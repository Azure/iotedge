namespace Microsoft.Azure.Devices.Edge.Hub.AuthAgent
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
 
    public class AuthRequest
    {
        [JsonProperty("version", Required = Required.Always)]
        public string Version { get; set; }

        [JsonProperty("username", Required = Required.Always)]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("certificate")]
        public string EncodedCertificateChain { get; set; }         
    }
}
