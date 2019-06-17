// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class Platform
    {
        public static Task<IEnumerable<string>> CollectLogsAsync(DateTime testStartTime, string filePrefix, CancellationToken token) => IsWindows()
            ? Windows.Logs.CollectAsync(testStartTime, filePrefix, token)
            : throw new NotImplementedException();

        // TODO: download installer script from aka.ms if user doesn't pass installerPath in Windows
        public static IEdgeDaemon CreateEdgeDaemon(Option<string> installerPath) => IsWindows()
            ? new Windows.EdgeDaemon(installerPath.Expect(() => new ArgumentException()))
            : new Linux.EdgeDaemon() as IEdgeDaemon;

        public static string GetConfigYamlPath() => IsWindows()
            ? @"C:\ProgramData\iotedge\config.yaml"
            : "/etc/iotedge/config.yaml";

        public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
}
