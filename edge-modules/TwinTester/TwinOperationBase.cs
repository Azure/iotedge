// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Extensions.Logging;

    public abstract class TwinOperationBase
    {
        TwinState TwinState { get; set; }
        AnalyzerClient AnalyzerClient { get; set; }
        static readonly ILogger Logger = ModuleUtil.CreateLogger("TwinTester");

        public abstract Task PerformUpdate();

        public abstract Task PerformValidation();

        protected bool IsPastFailureThreshold(DateTime twinUpdateTime)
        {
            DateTime comparisonPoint = new DateTime(Math.Max(twinUpdateTime.Ticks, this.TwinState.LastTimeOffline.Ticks));
            return DateTime.UtcNow - comparisonPoint > Settings.Current.TwinUpdateFailureThreshold;
        }

        protected async Task CallAnalyzerToReportStatus(string moduleId, string status, string responseJson)
        {
            try
            {
                await this.AnalyzerClient.AddTwinStatusAsync(new ResponseStatus { ModuleId = moduleId, StatusCode = status, ResultAsJson = responseJson, EnqueuedDateTime = DateTime.UtcNow });
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed call to report status to analyzer: {e}");
            }
        }
    }
}