// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.E2E.Test
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Newtonsoft.Json;

    public class ImagePullPolicyTestConfig
    {
        [JsonConstructor]
        public ImagePullPolicyTestConfig(ImagePullPolicy imagePullPolicy, bool pullImage)
        {
            this.ImagePullPolicy = imagePullPolicy;
            this.PullImage = pullImage;
        }

        [JsonProperty("imagePullPolicy")]
        public ImagePullPolicy ImagePullPolicy { get; set; }

        [JsonProperty("pullImage")]
        public bool PullImage { get; set; }
    }
}
