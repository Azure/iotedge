// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class ManifestSettings
    {
        public Option<string> ManifestSigningDeploymentPath;

        public Option<string> ManifestSigningSignedDeploymentPath;

        public Option<string> ManifestSigningRootCaPath;

        public Option<string> ManifestSignerClientBinPath;

        public ManifestSettings(Option<string> manifestSigningDeploymenPath, Option<string> manifestSigningSignedDeploymenPath, Option<string> manifestSigningRootCaPath, Option<string> manifestSignerClientBinPath)
        {
            this.ManifestSigningDeploymentPath = manifestSigningDeploymenPath;
            this.ManifestSigningSignedDeploymentPath = manifestSigningSignedDeploymenPath;
            this.ManifestSigningRootCaPath = manifestSigningRootCaPath;
            this.ManifestSignerClientBinPath = manifestSignerClientBinPath;
        }
    }
}
