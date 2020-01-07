// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using TestResultCoordinator.Report;
    using TestResultCoordinator.Storage;

    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : Controller
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(ReportController));
        readonly ITestOperationResultStorage storage;

        public ReportController(ITestOperationResultStorage storage)
        {
            this.storage = Preconditions.CheckNotNull(storage);
        }

        // GET api/report
        [HttpGet]
        public async Task<ContentResult> GetReportsAsync()
        {
            ITestResultReport[] testResultReports = await TestReportHelper.GenerateTestResultReports(this.storage, Logger);

            if (testResultReports.Length == 0)
            {
                return new ContentResult { Content = "No test report generated" };
            }

            return new ContentResult
            {
                Content = this.AddManualRunReportingHeading(JsonConvert.SerializeObject(testResultReports, Formatting.Indented)) // explicit serialization needed due to the wrapping list
            };
        }

        string AddManualRunReportingHeading(string reportContent)
        {
            return $"Run manually at {DateTime.UtcNow}{Environment.NewLine}Report result is not complete as testing is still running.{Environment.NewLine}{reportContent}";
        }
    }
}
