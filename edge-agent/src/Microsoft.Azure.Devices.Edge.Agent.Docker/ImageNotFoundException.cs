// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;

    class ImageNotFoundException : Exception
    {
        public ImageNotFoundException(string imageName, string imageTag, string dockerApiStatusCode, Exception innerException)
            : base($"Docker API responded with status code={dockerApiStatusCode}, image={imageName}, tag={imageTag}", innerException)
        {
        }
    }
}
