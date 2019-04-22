// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    using System;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class StreamRequestHandlerProvider : IStreamRequestHandlerProvider
    {
        const string LogsStreamName = "Logs";
        readonly ILogsProvider logsProvider;

        public StreamRequestHandlerProvider(ILogsProvider logsProvider)
        {
            this.logsProvider = Preconditions.CheckNotNull(logsProvider, nameof(logsProvider));
        }

        public bool TryGetHandler(string requestName, out IStreamRequestHandler handler)
        {
            if (requestName.Equals(LogsStreamName, StringComparison.OrdinalIgnoreCase))
            {
                handler = new LogsStreamRequestHandler(this.logsProvider);
                return true;
            }

            handler = null;
            return false;
        }
    }
}
