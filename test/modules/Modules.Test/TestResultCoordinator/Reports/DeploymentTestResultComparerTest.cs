// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using global::TestResultCoordinator.Reports;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Xunit;

    public class DeploymentTestResultComparerTest
    {
        static string expectedResultA = "{ \"TrackingId\": \"fb0f0a8b-d420-4dd1-8fbb-639e2e5d3863\", \"EnvironmentVariables\": { \"Env_Key1\": \"Evn_Value1\", \"Env_Key2\": \"Evn_Value2\" }, \"Source\": \"source1\", \"ResultType\": 6, \"CreatedAt\": \"2020-01-03T19:28:17Z\" }";
        static string actualResultA = "{ \"TrackingId\": \"fb0f0a8b-d420-4dd1-8fbb-639e2e5d3863\", \"EnvironmentVariables\": { \"Env_Key1\": \"Evn_Value1\", \"Env_Key2\": \"Evn_Value2\" }, \"Source\": \"source2\", \"ResultType\": 6, \"CreatedAt\": \"2020-01-03T20:38:00Z\" }";

        static string expectedResultB = "{ \"TrackingId\": \"fb0f0a8b-d420-4dd1-8fbb-639e2e5d3863\", \"EnvironmentVariables\": { \"Env_Key1\": \"Evn_Value1\" }, \"Source\": \"source1\", \"ResultType\": 6, \"CreatedAt\": \"2020-01-03T19:28:17Z\" }";
        static string actualResultB = "{ \"TrackingId\": \"fb0f0a8b-d420-4dd1-8fbb-639e2e5d3863\", \"EnvironmentVariables\": { \"Env_Key1\": \"Evn_Value1\", \"Env_Key2\": \"Evn_Value2\" }, \"Source\": \"source2\", \"ResultType\": 6, \"CreatedAt\": \"2020-01-03T20:38:00Z\" }";

        static string expectedResultC = "{ \"TrackingId\": \"fb0f0a8b-d420-4dd1-8fbb-639e2e5d3863\", \"EnvironmentVariables\": { \"Env_Key1\": \"Evn_Value1\", \"Env_Key2\": \"Evn_Value2\" }, \"Source\": \"source1\", \"ResultType\": 6, \"CreatedAt\": \"2020-01-03T19:28:17Z\" }";
        static string actualResultC = "{ \"TrackingId\": \"fb0f0a8b-d420-4dd1-8fbb-639e2e5d3863\", \"EnvironmentVariables\": { \"Env_Key1\": \"Evn_Value1\", \"Env_DiffKey\": \"Evn_Value2\" }, \"Source\": \"source2\", \"ResultType\": 6, \"CreatedAt\": \"2020-01-03T20:38:00Z\" }";

        static string expectedResultD = "{ \"TrackingId\": \"fb0f0a8b-d420-4dd1-8fbb-639e2e5d3863\", \"EnvironmentVariables\": { \"Env_Key1\": \"Evn_Value1\", \"Env_Key2\": \"Evn_Value2\" }, \"Source\": \"source1\", \"ResultType\": 6, \"CreatedAt\": \"2020-01-03T19:28:17Z\" }";
        static string actualResultD = "{ \"TrackingId\": \"fb0f0a8b-d420-4dd1-8fbb-639e2e5d3863\", \"EnvironmentVariables\": { \"Env_Key1\": \"Evn_Value1\", \"Env_Key2\": \"Evn_DiffValue\" }, \"Source\": \"source2\", \"ResultType\": 6, \"CreatedAt\": \"2020-01-03T20:38:00Z\" }";

        static string expectedResultE = "{ \"TrackingId\": \"fb0f0a8b-d420-4dd1-8fbb-639e2e5d3863\", \"EnvironmentVariables\": { \"Env_Key1\": \"Evn_Value1\", \"Env_Key2\": \"Evn_Value2\" }, \"Source\": \"source2\", \"ResultType\": 6, \"CreatedAt\": \"2020-01-03T20:38:00Z\" }";
        static string actualResultE = "{ \"TrackingId\": \"fb0f0a8b-d420-4dd1-8fbb-639e2e5d3863\", \"EnvironmentVariables\": { \"Env_Key1\": \"Evn_Value1\" }, \"Source\": \"source1\", \"ResultType\": 6, \"CreatedAt\": \"2020-01-03T19:28:17Z\" }";

        public static IEnumerable<object[]> GetTestDataForMatches =>
            new List<object[]>
            {
                new object[] { "source1", "resultType1", expectedResultA, "source2", "resultType1", actualResultA, true },
                new object[] { "source1", "resulTTypE1", expectedResultA, "source2", "resultType1", actualResultA, true },
                new object[] { "source1", "resultType1", expectedResultB, "source2", "resultType1", actualResultB, true },
                new object[] { "source1", "resultType1", expectedResultA, "source2", "resultType2", actualResultA, false },
                new object[] { "source1", "resultType1", expectedResultC, "source2", "resultType1", actualResultC, false },
                new object[] { "source1", "resultType1", expectedResultD, "source2", "resultType1", actualResultD, false },
                new object[] { "source1", "resultType1", expectedResultE, "source2", "resultType1", actualResultE, false },
            };

        [Theory]
        [MemberData(nameof(GetTestDataForMatches))]
        public void TestMatches(string source1, string resultType1, string value1, string source2, string resultType2, string value2, bool isMatched)
        {
            var expected = new TestOperationResult(source1, resultType1, value1, DateTime.UtcNow);
            var actual = new TestOperationResult(source2, resultType2, value2, DateTime.UtcNow);

            Assert.Equal(isMatched, new DeploymentTestResultComparer().Matches(expected, actual));
        }
    }
}
