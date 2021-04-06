// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    [Serializable]
    public class DeploymentManifestIsNotSignedException : Exception
    {
        public DeploymentManifestIsNotSignedException(string message)
            : base(message)
        {
        }

        public DeploymentManifestIsNotSignedException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
