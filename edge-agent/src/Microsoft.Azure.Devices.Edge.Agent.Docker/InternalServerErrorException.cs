// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;

    class InternalServerErrorException : Exception
    {
        public String ImageName { get; }

        public String ImageTag { get; }

        public String DockerApiStatusCode { get; }

        public InternalServerErrorException(String imageName, String imageTag, string dockerApiStatusCode, Exception innerException)
            : base($"Docker API responded with status code={dockerApiStatusCode}, image={imageName}, tag={imageTag}. Check container registry (possible authorization failure or container registry down).", innerException)
        {
            this.ImageName = imageName;
            this.ImageTag = imageTag;
            this.DockerApiStatusCode = dockerApiStatusCode;
        }
    }
}
