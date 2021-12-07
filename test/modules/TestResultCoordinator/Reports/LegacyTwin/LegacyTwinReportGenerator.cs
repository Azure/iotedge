// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.LegacyTwin
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.Reports;

    sealed class LegacyTwinReportGenerator : ITestResultReportGenerator
    {
        const double BigToleranceProportion = .005;
        const double LittleToleranceProportion = .001;
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(LegacyTwinReportGenerator));
        readonly string trackingId;

        internal LegacyTwinReportGenerator(
            string testDescription,
            string trackingId,
            string resultType,
            string senderSource,
            Topology topology,
            bool mqttBrokerEnabled,
            IAsyncEnumerator<TestOperationResult> senderTestResults)
        {
            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.SenderTestResults = Preconditions.CheckNotNull(senderTestResults, nameof(senderTestResults));
            this.Topology = topology;
        }

        internal string TestDescription { get; }

        internal string ResultType { get; }

        internal string SenderSource { get; }

        internal IAsyncEnumerator<TestOperationResult> SenderTestResults { get; }

        internal Topology Topology { get; }

        internal bool MqttBrokerEnabled { get; }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Start to generate report by {nameof(LegacyTwinReportGenerator)} for Sources [{this.SenderSource}] ");
            IDictionary<int, int> results = new Dictionary<int, int>();
            bool isPassed = true;
            while (await this.SenderTestResults.MoveNextAsync())
            {
                int status = int.Parse(this.SenderTestResults.Current.Result.Substring(0, 3));
                if (status > 299)
                {
                    isPassed = false;
                }

                if (results.ContainsKey(status))
                {
                    results[status] = results[status] + 1;
                }
                else
                {
                    results[status] = 1;
                }
            }

            var report = new LegacyTwinReport(
                this.TestDescription,
                this.trackingId,
                this.ResultType,
                this.SenderSource,
                results,
                isPassed);

            Logger.LogInformation($"Successfully finished creating LegacyTwinReport for Source [{this.SenderSource}]");
            return report;
        }

        // See TwinTester/StatusCode.cs for reference.
        bool IsPassed(IDictionary<int, int> statusCodesToCount)
        {
            bool isPassed = true;
            int totalResults = statusCodesToCount.Sum(x => x.Value);

            if (totalResults == 0)
            {
                return false;
            }

            // Product issue where:
            // 1) We don't receive some desired properties in module-registered twin desired property callback
            // 1) Module cannot make reported property update
            if (this.Topology == Topology.Nested && this.MqttBrokerEnabled)
            {
                int[] bigToleranceStatusCodes = { };
                int[] littleToleranceStatusCodes = { 501, 504 };
                isPassed = this.GeneratePassResult(statusCodesToCount, bigToleranceStatusCodes, littleToleranceStatusCodes);
            }
            else
            {
                List<int> statusCodes = statusCodesToCount.Keys.ToList();
                IEnumerable<int> failingStatusCodes = statusCodes.Where(s =>
                {
                    string statusCode = s.ToString();
                    return !statusCode.StartsWith("2");
                });

                isPassed = failingStatusCodes.Count() == 0;
            }

            return isPassed;
        }

        bool GeneratePassResult(IDictionary<int, int> statusCodesToCount, int[] bigToleranceStatusCodes, int[] littleToleranceStatusCodes)
        {
            int totalResults = statusCodesToCount.Sum(x => x.Value);
            foreach (KeyValuePair<int, int> statusCodeToCount in statusCodesToCount)
            {
                int statusCode = statusCodeToCount.Key;
                int statusCodeCount = statusCodeToCount.Value;

                // ignore the status codes indicating some success
                if (statusCode.ToString().StartsWith("2"))
                {
                    continue;
                }
                else if (bigToleranceStatusCodes.Contains(statusCode))
                {
                    if ((double)statusCodeCount / totalResults > BigToleranceProportion)
                    {
                        return false;
                    }
                }
                else if (littleToleranceStatusCodes.Contains(statusCode))
                {
                    if ((double)statusCodeCount / totalResults > LittleToleranceProportion)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
    }
}
