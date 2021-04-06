// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    [Serializable]
    public class ManifestSigningIsNotEnabledProperly : Exception
    {
        public ManifestSigningIsNotEnabledProperly(string message)
            : base(message)
        {
        }

        public ManifestSigningIsNotEnabledProperly(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
