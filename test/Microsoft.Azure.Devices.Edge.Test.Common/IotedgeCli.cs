// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class IotedgeCli
    {
        readonly string name;

        public IotedgeCli(string name = "iotedge")
        {
            this.name = name;
        }

        public Task RunAsync(
            string args,
            Action<string> onStandardOutput,
            Action<string> onStandardError,
            CancellationToken token,
            bool logCommand = true)
        {
            return Process.RunAsync(this.name, args, onStandardOutput, onStandardError, token, logCommand);
        }

        public Task<string[]> RunAsync(
            string args,
            CancellationToken token,
            bool logCommand = true,
            bool logOutput = true)
        {
            return Process.RunAsync(this.name, args, token, logCommand, logOutput);
        }
    }
}