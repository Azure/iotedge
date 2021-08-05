// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class ManifestSettings
    {
        public Option<string> ManifestSigningDeploymentDir;

        public Option<string> ManifestSigningRootCaPath;

        public Option<string> ManifestSigningLaunchSettingsPath;

        public ManifestSettings(Option<string> manifestSigningDeploymentDir, Option<string> manifestSigningRootCaPath, Option<string> manifestSigningLaunchSettingsPath)
        {
            this.ManifestSigningDeploymentDir = manifestSigningDeploymentDir;
            this.ManifestSigningRootCaPath = manifestSigningRootCaPath;
            this.ManifestSigningLaunchSettingsPath = manifestSigningLaunchSettingsPath;
        }
    }
}
