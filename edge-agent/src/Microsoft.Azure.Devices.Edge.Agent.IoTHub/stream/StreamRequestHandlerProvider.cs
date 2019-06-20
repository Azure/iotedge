// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    using System;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class StreamRequestHandlerProvider : IStreamRequestHandlerProvider
    {
        const string LogsStreamName = "Logs";
        readonly ILogsProvider logsProvider;
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public StreamRequestHandlerProvider(ILogsProvider logsProvider, IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.logsProvider = Preconditions.CheckNotNull(logsProvider, nameof(logsProvider));
            this.runtimeInfoProvider = Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider));
        }

        public bool TryGetHandler(string requestName, out IStreamRequestHandler handler)
        {
            if (requestName.Equals(LogsStreamName, StringComparison.OrdinalIgnoreCase))
            {
                handler = new LogsStreamRequestHandler(this.logsProvider, this.runtimeInfoProvider);
                return true;
            }

            handler = null;
            return false;
        }
    }
}
