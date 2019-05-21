// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System.Runtime.InteropServices;

    public static class Platform
    {
        public static IEdgeDaemon CreateEdgeDaemon(string installerPath) => IsWindows()
            ? new Windows.EdgeDaemon(installerPath)
            : new Linux.EdgeDaemon() as IEdgeDaemon;

        public static string GetConfigYamlPath() => IsWindows()
            ? @"C:\ProgramData\iotedge\config.yaml"
            : "/etc/iotedge/config.yaml";

        public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
}
