// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    [JsonConverter(typeof(ImagePullSecretNameConverter))]
    public class ImagePullSecretName
    {
        readonly string value;

        public ImagePullSecretName(string name)
        {
            this.value = Preconditions.CheckNonWhiteSpace(name, nameof(name));
        }

        public static ImagePullSecretName Create(AuthConfig auth) => new ImagePullSecretName($"{auth.Username.ToLower()}-{auth.ServerAddress.ToLower()}");

        public static implicit operator string(ImagePullSecretName name) => name.ToString();

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((ImagePullSecretName)obj);
        }

        bool Equals(ImagePullSecretName other) => this.value.Equals(other.value);

        public override int GetHashCode() => this.ToString().GetHashCode();

        public override string ToString() => this.value;
    }
}
