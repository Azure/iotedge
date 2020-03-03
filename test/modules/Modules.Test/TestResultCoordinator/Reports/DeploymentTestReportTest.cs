// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using global::TestResultCoordinator.Reports;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class DeploymentTestReportTest
    {
        [Fact]
        public void TestConstructorSuccess()
        {
            DeploymentTestResult testResult = GetDeploymentTestResult();

            var report = new DeploymentTestReport(
                "trackingId123",
                "expectedSource",
                "actualSource",
                "resultType1",
                15,
                10,
                13,
                Option.Some(testResult.ToTestOperationResult()),
                new List<TestOperationResult>
                {
                    new TestOperationResult("expectedSource", "resultType1", "FakeValue1", new DateTime(2019, 12, 4, 10, 15, 15)),
                    new TestOperationResult("expectedSource", "resultType1", "FakeValue2", new DateTime(2019, 12, 4, 10, 15, 18)),
                });

            Assert.Equal("trackingId123", report.TrackingId);
            Assert.Equal("actualSource", report.ActualSource);
            Assert.Equal("expectedSource", report.ExpectedSource);
            Assert.Equal("resultType1", report.ResultType);
            Assert.Equal(15UL, report.TotalExpectedDeployments);
            Assert.Equal(10UL, report.TotalActualDeployments);
            Assert.Equal(13UL, report.TotalMatchedDeployments);
            Assert.Equal(2, report.UnmatchedResults.Count);

            Assert.Equal("expectedSource", report.UnmatchedResults[0].Source);
            Assert.Equal("resultType1", report.UnmatchedResults[0].Type);
            Assert.Equal("FakeValue1", report.UnmatchedResults[0].Result);
            Assert.Equal(new DateTime(2019, 12, 4, 10, 15, 15), report.UnmatchedResults[0].CreatedAt);

            Assert.Equal("expectedSource", report.UnmatchedResults[1].Source);
            Assert.Equal("resultType1", report.UnmatchedResults[1].Type);
            Assert.Equal("FakeValue2", report.UnmatchedResults[1].Result);
            Assert.Equal(new DateTime(2019, 12, 4, 10, 15, 18), report.UnmatchedResults[1].CreatedAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTrackingIdIsNotProvided(string trackingId)
        {
            DeploymentTestResult testResult = GetDeploymentTestResult();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DeploymentTestReport(
                    trackingId,
                    "expectedSource",
                    "actualSource",
                    "resultType1",
                    15,
                    10,
                    13,
                    Option.Some(testResult.ToTestOperationResult()),
                    new List<TestOperationResult>
                    {
                        new TestOperationResult("expectedSource", "resultType1", "FakeValue1", new DateTime(2019, 12, 4, 10, 15, 15)),
                        new TestOperationResult("expectedSource", "resultType1", "FakeValue2", new DateTime(2019, 12, 4, 10, 15, 18)),
                    }));

            Assert.StartsWith("trackingId", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenExpectedSourceIsNotProvided(string expectedSource)
        {
            DeploymentTestResult testResult = GetDeploymentTestResult();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DeploymentTestReport(
                    "trackingId123",
                    expectedSource,
                    "actualSource",
                    "resultType1",
                    15,
                    10,
                    13,
                    Option.Some(testResult.ToTestOperationResult()),
                    new List<TestOperationResult>
                    {
                        new TestOperationResult("expectedSource", "resultType1", "FakeValue1", new DateTime(2019, 12, 4, 10, 15, 15)),
                        new TestOperationResult("expectedSource", "resultType1", "FakeValue2", new DateTime(2019, 12, 4, 10, 15, 18)),
                    }));

            Assert.StartsWith("expectedSource", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenActualSourceIsNotProvided(string actualSource)
        {
            DeploymentTestResult testResult = GetDeploymentTestResult();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DeploymentTestReport(
                    "trackingId123",
                    "expectedSource",
                    actualSource,
                    "resultType1",
                    15,
                    10,
                    13,
                    Option.Some(testResult.ToTestOperationResult()),
                    new List<TestOperationResult>
                    {
                        new TestOperationResult("expectedSource", "resultType1", "FakeValue1", new DateTime(2019, 12, 4, 10, 15, 15)),
                        new TestOperationResult("expectedSource", "resultType1", "FakeValue2", new DateTime(2019, 12, 4, 10, 15, 18)),
                    }));

            Assert.StartsWith("actualSource", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenResultTypeIsNotProvided(string resultType)
        {
            DeploymentTestResult testResult = GetDeploymentTestResult();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DeploymentTestReport(
                    "trackingId123",
                    "expectedSource",
                    "actualSource",
                    resultType,
                    15,
                    10,
                    13,
                    Option.Some(testResult.ToTestOperationResult()),
                    new List<TestOperationResult>
                    {
                        new TestOperationResult("expectedSource", "resultType1", "FakeValue1", new DateTime(2019, 12, 4, 10, 15, 15)),
                        new TestOperationResult("expectedSource", "resultType1", "FakeValue2", new DateTime(2019, 12, 4, 10, 15, 18)),
                    }));

            Assert.StartsWith("resultType", ex.Message);
        }

        private static DeploymentTestResult GetDeploymentTestResult()
        {
            var envVars = new Dictionary<string, string>();

            for (int i = 1; i <= 13; i++)
            {
                envVars.Add($"Env_Key{i}", $"Env_Value{i}");
            }

            return new DeploymentTestResult("trackingId123", "actualSource", envVars, DateTime.UtcNow);
        }
    }
}
