// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    [Serializable]
    public class ManifestTrustBundleIsNotConfiguredException : Exception
    {
        public ManifestTrustBundleIsNotConfiguredException(string message)
            : base(message)
        {
        }

        public ManifestTrustBundleIsNotConfiguredException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
