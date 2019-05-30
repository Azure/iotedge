// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class Platform
    {
        public static IEdgeDaemon CreateEdgeDaemon(Option<string> installerPath) => IsWindows()
            ? new Windows.EdgeDaemon(installerPath.Expect(() => new ArgumentException()))
            : new Linux.EdgeDaemon() as IEdgeDaemon;

        public static string GetConfigYamlPath() => IsWindows()
            ? @"C:\ProgramData\iotedge\config.yaml"
            : "/etc/iotedge/config.yaml";

        public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
}
