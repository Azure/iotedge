

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using global::Docker.DotNet.Models;
    using Newtonsoft.Json;
    using Org.BouncyCastle.Asn1;
    using Org.BouncyCastle.Bcpg;
    using Org.BouncyCastle.Utilities.Encoders;

    class ImagePullSecret
    {
        class AuthEntry
        {
            [JsonProperty(Required = Required.Always, PropertyName = "username")]
            public readonly string Username;
            [JsonProperty(Required = Required.Always, PropertyName = "password")]
            public readonly string Password;
            [JsonProperty(Required = Required.Always, PropertyName = "auth")]
            public readonly string Auth;

            public AuthEntry(string username, string password)
            {
                this.Username = username;
                this.Password = password;
                byte[] auth = Encoding.UTF8.GetBytes($"{username}:{password}");
                this.Auth = Convert.ToBase64String(auth);
            }
        }
        class Auth
        {
            [JsonProperty(Required = Required.Always, PropertyName = "auths")]
            public Dictionary<string, AuthEntry> Auths;

            public Auth()
            {
                this.Auths = new Dictionary<string, AuthEntry>();
            }

            public Auth(string registry, AuthEntry entry) :
                this()
            {
                this.Auths.Add(registry,entry);
            }
        }
        public string Name { get;}

        readonly AuthConfig dockerAuth;

        public string GenerateSecret()
        {
            // JSON struct is
            // { "auths":
            //   { "<registry>" :
            //     { "username":"<user>",
            //       "password":"<password>",
            //       "email":"<email>" (not needed)
            //       "auth":"<base 64 of '<user>:<password>'>"
            //     }
            //   }
            // }
            var auths = new Auth(this.dockerAuth.ServerAddress,
                              new AuthEntry(this.dockerAuth.Username,this.dockerAuth.Password));
            string authString = JsonConvert.SerializeObject(auths);
            return authString;
        }
        public ImagePullSecret(AuthConfig dockerAuth)
        {
            this.dockerAuth = dockerAuth;
            this.Name = $"{dockerAuth.Username.ToLower()}-{dockerAuth.ServerAddress.ToLower()}";
        }
    }
}
