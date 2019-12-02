// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Extensions.Logging;

    public abstract class TwinOperationBase
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinOperationBase));

        public abstract Task UpdateAsync();

        public abstract Task ValidateAsync();

        protected static bool IsPastFailureThreshold(TwinState twinState, DateTime twinUpdateTime)
        {
            DateTime comparisonPoint = new DateTime(Math.Max(twinUpdateTime.Ticks, twinState.LastTimeOffline.Ticks));
            return DateTime.UtcNow - comparisonPoint > Settings.Current.TwinUpdateFailureThreshold;
        }

        protected static async Task CallAnalyzerToReportStatusAsync(AnalyzerClient analyzerClient, string moduleId, string status)
        {
            try
            {
                await analyzerClient.AddTwinStatusAsync(new ResponseStatus { ModuleId = moduleId, StatusCode = status, EnqueuedDateTime = DateTime.UtcNow });
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed call to report status to analyzer: {e}");
            }
        }
    }
}