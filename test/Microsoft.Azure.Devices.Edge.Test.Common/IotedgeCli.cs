// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class IotedgeCli
    {
        public static Task RunAsync(string args, Action<string> onStandardOutput, Action<string> onStandardError, CancellationToken token)
        {
            return Process.RunAsync("iotedge", args, onStandardOutput, onStandardError, token);
        }

        public static Task<string[]> RunAsync(string args, CancellationToken token, bool logVerbose = true)
        {
            return Process.RunAsync("iotedge", args, token, logVerbose);
        }
    }
}