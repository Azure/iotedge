// Copyright (c) Microsoft. All rights reserved.
namespace CloudToDeviceMessageTester
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    abstract class CloudToDeviceMessageTesterBase : IDisposable
    {
        protected readonly ILogger logger;
        protected readonly string iotHubConnectionString;
        protected readonly string deviceId;
        protected readonly string moduleId;
        protected readonly TransportType transportType;
        protected readonly TimeSpan testDuration;
        protected readonly TestResultReportingClient testResultReportingClient;

        protected CloudToDeviceMessageTesterBase(
            ILogger logger,
            string iotHubConnectionString,
            string deviceId,
            string moduleId,
            TransportType transportType,
            TimeSpan testDuration,
            TestResultReportingClient testResultReportingClient)
        {
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.iotHubConnectionString = Preconditions.CheckNonWhiteSpace(iotHubConnectionString, nameof(iotHubConnectionString));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.transportType = transportType;
            this.testDuration = testDuration;
            this.testResultReportingClient = Preconditions.CheckNotNull(testResultReportingClient, nameof(testResultReportingClient));
        }

        public abstract Task StartAsync(CancellationToken ct);

        public abstract void Dispose();
    }
}
