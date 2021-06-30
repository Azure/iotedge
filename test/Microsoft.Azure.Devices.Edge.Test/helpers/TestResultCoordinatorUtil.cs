// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using Serilog;

    public class TestResultCoordinatorUtil
    {
        const string TestResultCoordinatorUrl = "http://testResultCoordinator:5001";
        const string TrcModuleName = "testResultCoordinator";
        const string NetworkControllerModuleName = "networkController";

        public static Action<EdgeConfigBuilder> BuildAddNetworkControllerConfig(string trackingId, string networkControllerImage)
        {
            return new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.AddModule(NetworkControllerModuleName, networkControllerImage)
                        .WithEnvironment(new[]
                        {
                            ("trackingId", trackingId),
                            ("testResultCoordinatorUrl", TestResultCoordinatorUrl),
                            ("RunFrequencies__0__OfflineFrequency", "00:00:00"),
                            ("RunFrequencies__0__OnlineFrequency", "00:00:00"),
                            ("RunFrequencies__0__RunsCount", "0"),
                            ("NetworkControllerRunProfile", "Online"),
                            ("StartAfter", "00:00:00")
                        })
                        .WithSettings(new[] { ("createOptions", "{\"HostConfig\":{\"Binds\":[\"/var/run/docker.sock:/var/run/docker.sock\"], \"NetworkMode\":\"host\", \"Privileged\":true},\"NetworkingConfig\":{\"EndpointsConfig\":{\"host\":{}}}}") });
                });
        }

        public static Action<EdgeConfigBuilder> BuildAddTestResultCoordinatorConfig(string trackingId, string trcImage, string expectedSourceModuleName, string actualSourceModuleName)
        {
            return new Action<EdgeConfigBuilder>(
                builder =>
                {
                    // This test uses the TestResultCoordinator. It was originally designed for connectivity tests, so some of the parameters
                    // are unnecessary for the e2e tests.
                    // Note: We cannot bind ports to the host for squid proxy tests, as squid will deny aspnetcore's GET request to init this bind
                    builder.AddModule(TrcModuleName, trcImage)
                       .WithEnvironment(new[]
                       {
                           ("trackingId", trackingId),
                           ("useTestResultReportingService", "false"),
                           ("useResultEventReceivingService", "false"),
                           ("IOT_HUB_CONNECTION_STRING", Context.Current.ConnectionString),
                           ("testStartDelay", "00:00:00"),
                           ("verificationDelay", "00:00:00"),
                           ("NetworkControllerRunProfile", "Online"),
                           ("TEST_INFO", "key=unnecessary")
                       })

                       .WithDesiredProperties(new Dictionary<string, object>
                       {
                           ["reportMetadataList"] = new Dictionary<string, object>
                           {
                               ["reportMetadata1"] = new Dictionary<string, object>
                               {
                                   ["TestReportType"] = "CountingReport",
                                   ["TestOperationResultType"] = "Messages",
                                   ["ExpectedSource"] = $"{expectedSourceModuleName}.send",
                                   ["ActualSource"] = $"{actualSourceModuleName}.receive",
                                   ["TestDescription"] = "unnecessary"
                               },
                               ["reportMetadata2"] = new Dictionary<string, object>
                               {
                                   ["TestReportType"] = "NetworkControllerReport",
                                   ["Source"] = $"{NetworkControllerModuleName}",
                                   ["TestDescription"] = "network controller"
                               }
                           }
                       })
                       .WithProxy(Context.Current.TestRunnerProxy);
                });
        }

        public static async Task ValidateResultsAsync()
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(TestResultCoordinatorUrl);
            Log.Information("Test Result Coordinator response: {Response}", response.ToString());
            var jsonstring = await response.Content.ReadAsStringAsync();
            Log.Information("Test Result Coordinator response: {Response}", jsonstring);

            bool isPassed = (bool)JArray.Parse(jsonstring)[0]["IsPassed"];

            Assert.IsTrue(isPassed);
        }
    }
}