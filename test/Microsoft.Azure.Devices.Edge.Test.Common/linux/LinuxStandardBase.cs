// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using Microsoft.Azure.Devices.Edge.Util;
    using SupportedPackageExtension = PackageManagement.SupportedPackageExtension;

    class LinuxStandardBase
    {
        public string Os { get; }
        public string Version { get; }
        public SupportedPackageExtension PackageExtension { get; }

        public LinuxStandardBase(string os, string version, SupportedPackageExtension packageExtension)
        {
            Preconditions.CheckNonWhiteSpace(os, nameof(os));
            Preconditions.CheckNonWhiteSpace(version, nameof(version));

            this.Os = os;
            this.Version = version;
            this.PackageExtension = packageExtension;
        }
    }
}