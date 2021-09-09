// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    [Serializable]
    public class ManifestTrustBundleChainingFailedException : Exception
    {
        public ManifestTrustBundleChainingFailedException(string message)
            : base(message)
        {
        }

        public ManifestTrustBundleChainingFailedException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
