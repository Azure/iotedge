// Copyright (c) Microsoft. All rights reserved.
namespace DebugNetworkStatusChange
{
    using System;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    class NonStaticClass
    {
        ILogger logger = null;

        public NonStaticClass(ILogger logger)
        {
            this.logger = logger;
        }

        public void statusChangedHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            this.logger.LogInformation($"Connection status changed to {status} with reason {reason}");
        }
    }
}
