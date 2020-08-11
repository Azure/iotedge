// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Logs
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class LogsRequestToOptionsMapperTests
    {
        public static IEnumerable<object[]> GetMatchingIdsTestData()
        {
            yield return new object[]
            {
                "edge",
                new List<string> { "edgehub", "edgeAgent", "module1", "edgMod2" },
                new List<string> { "edgehub", "edgeAgent" },
            };

            yield return new object[]
            {
                "e.*t",
                new List<string> { "edgehub", "edgeAgent", "module1", "eandt" },
                new List<string> { "edgeAgent", "eandt" },
            };

            yield return new object[]
            {
                "EDGE",
                new List<string> { "edgehub", "edgeAgent", "module1", "testmod3" },
                new List<string> { "edgehub", "edgeAgent" },
            };

            yield return new object[]
            {
                "^e.*",
                new List<string> { "edgehub", "edgeAgent", "module1", "eandt" },
                new List<string> { "edgehub", "edgeAgent", "eandt" },
            };
        }

        public static IEnumerable<object[]> GetIdsToProcessTestData()
        {
            var logOptions1 = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Json, ModuleLogFilter.Empty, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);
            var logOptions2 = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, ModuleLogFilter.Empty, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);
            var logOptions3 = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(100), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.None<string>()), LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);

            yield return new object[]
            {
                new List<(string id, ModuleLogOptions logOptions)>
                {
                    ("edgeAgent", logOptions1),
                    ("edgeHub", logOptions2),
                    ("tempSensor", logOptions3)
                },
                new List<string> { "edgeAgent", "edgeHub", "tempSensor", "eModule2" },
                new Dictionary<string, ModuleLogOptions>
                {
                    ["edgeAgent"] = logOptions1,
                    ["edgeHub"] = logOptions2,
                    ["tempSensor"] = logOptions3
                }
            };

            yield return new object[]
            {
                new List<(string id, ModuleLogOptions logOptions)>
                {
                    ("edgeAgent", logOptions1),
                    ("edgeHub", logOptions2),
                    ("tempSensor", logOptions3)
                },
                new List<string> { "edgeAgent", "edgeHub", "tempSimulator", "eModule2" },
                new Dictionary<string, ModuleLogOptions>
                {
                    ["edgeAgent"] = logOptions1,
                    ["edgeHub"] = logOptions2
                }
            };

            yield return new object[]
            {
                new List<(string id, ModuleLogOptions logOptions)>
                {
                    ("edge", logOptions1),
                    ("edgeHub", logOptions2),
                    ("e.*e", logOptions3)
                },
                new List<string> { "edgeAgent", "edgeHub", "module1", "eModule2" },
                new Dictionary<string, ModuleLogOptions>
                {
                    ["edgeAgent"] = logOptions1,
                    ["edgeHub"] = logOptions1,
                    ["eModule2"] = logOptions3
                }
            };

            yield return new object[]
            {
                new List<(string id, ModuleLogOptions logOptions)>
                {
                    ("^e.*", logOptions1),
                    ("mod", logOptions2)
                },
                new List<string> { "edgeAgent", "edgeHub", "module1", "eModule2" },
                new Dictionary<string, ModuleLogOptions>
                {
                    ["edgeAgent"] = logOptions1,
                    ["edgeHub"] = logOptions1,
                    ["eModule2"] = logOptions1,
                    ["module1"] = logOptions2,
                }
            };
        }

        [Theory]
        [MemberData(nameof(GetMatchingIdsTestData))]
        public void GetMatchingIdsTest(string regex, IList<string> moduleIds, IList<string> expectedList)
        {
            ISet<string> actualModules = LogsRequestToOptionsMapper.GetMatchingIds(regex, moduleIds);
            Assert.Equal(expectedList.OrderBy(i => i), actualModules.OrderBy(i => i));
        }

        [Theory]
        [MemberData(nameof(GetIdsToProcessTestData))]
        public void GetIdsToProcessTest(IList<(string id, ModuleLogOptions logOptions)> idList, IList<string> allIds, IDictionary<string, ModuleLogOptions> expectedIdsToProcess)
        {
            IDictionary<string, ModuleLogOptions> idsToProcess = LogsRequestToOptionsMapper.GetIdsToProcess(idList, allIds);
            Assert.Equal(expectedIdsToProcess, idsToProcess);
        }
    }
}
