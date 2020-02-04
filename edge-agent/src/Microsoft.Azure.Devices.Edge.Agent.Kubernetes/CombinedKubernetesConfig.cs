// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util;

    public class CombinedKubernetesConfig
    {
        public CombinedKubernetesConfig(string image, CreatePodParameters createOptions, Option<ImagePullSecret> imagePullSecret)
        {
            this.Image = Preconditions.CheckNonWhiteSpace(image, nameof(image)).Trim();
            this.CreateOptions = Preconditions.CheckNotNull(createOptions, nameof(createOptions));
            this.ImagePullSecret = imagePullSecret;
        }

        public string Image { get; }

        public CreatePodParameters CreateOptions { get; }

        public Option<ImagePullSecret> ImagePullSecret { get; }
    }
}
