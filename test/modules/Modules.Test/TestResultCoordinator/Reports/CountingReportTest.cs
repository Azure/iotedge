// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using global::TestResultCoordinator.Reports;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class CountingReportTest
    {
        static readonly string TestDescription = "dummy description";

        [Fact]
        public void TestConstructorSuccess()
        {
            var report = new CountingReport(
                TestDescription,
                "trackingId123",
                "expectedSource",
                "actualSource",
                "resultType1",
                945,
                923,
                33,
                new List<TestOperationResult>
                {
                    new TestOperationResult("expectedSource", "resultType1", "332", new DateTime(2019, 12, 4, 10, 15, 15)),
                    new TestOperationResult("expectedSource", "resultType1", "734", new DateTime(2019, 12, 4, 10, 15, 18)),
                },
                Option.None<EventHubSpecificReportComponents>(),
                Option.None<DateTime>());

            Assert.Equal(TestDescription, report.TestDescription);
            Assert.Equal("trackingId123", report.TrackingId);
            Assert.Equal("actualSource", report.ActualSource);
            Assert.Equal("expectedSource", report.ExpectedSource);
            Assert.Equal("resultType1", report.ResultType);
            Assert.Equal(945UL, report.TotalExpectCount);
            Assert.Equal(923UL, report.TotalMatchCount);
            Assert.Equal(33UL, report.TotalDuplicateResultCount);

            Assert.Equal("expectedSource", report.UnmatchedResults[0].Source);
            Assert.Equal("resultType1", report.UnmatchedResults[0].Type);
            Assert.Equal("332", report.UnmatchedResults[0].Result);
            Assert.Equal(new DateTime(2019, 12, 4, 10, 15, 15), report.UnmatchedResults[0].CreatedAt);

            Assert.Equal("expectedSource", report.UnmatchedResults[1].Source);
            Assert.Equal("resultType1", report.UnmatchedResults[1].Type);
            Assert.Equal("734", report.UnmatchedResults[1].Result);
            Assert.Equal(new DateTime(2019, 12, 4, 10, 15, 18), report.UnmatchedResults[1].CreatedAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTestDescriptionIsNotProvided(string testDescription)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new CountingReport(
                    testDescription,
                    "trackingId123",
                    "expectedSource",
                    "actualSource",
                    "resultType1",
                    945,
                    923,
                    33,
                    new List<TestOperationResult>
                    {
                        new TestOperationResult("expectedSource", "resultType1", "332", new DateTime(2019, 12, 4, 10, 15, 15)),
                        new TestOperationResult("expectedSource", "resultType1", "734", new DateTime(2019, 12, 4, 10, 15, 18)),
                    },
                    Option.None<EventHubSpecificReportComponents>(),
                    Option.None<DateTime>()));

            Assert.StartsWith("testDescription", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTrackingIdIsNotProvided(string trackingId)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new CountingReport(
                    TestDescription,
                    trackingId,
                    "expectedSource",
                    "actualSource",
                    "resultType1",
                    945,
                    923,
                    33,
                    new List<TestOperationResult>
                    {
                        new TestOperationResult("expectedSource", "resultType1", "332", new DateTime(2019, 12, 4, 10, 15, 15)),
                        new TestOperationResult("expectedSource", "resultType1", "734", new DateTime(2019, 12, 4, 10, 15, 18)),
                    },
                    Option.None<EventHubSpecificReportComponents>(),
                    Option.None<DateTime>()));

            Assert.StartsWith("trackingId", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenExpectedSourceIsNotProvided(string expectedSource)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new CountingReport(
                    TestDescription,
                    "trackingId-23434",
                    expectedSource,
                    "actualSource",
                    "resultType1",
                    945,
                    923,
                    33,
                    new List<TestOperationResult>
                    {
                        new TestOperationResult("expectedSource", "resultType1", "332", new DateTime(2019, 12, 4, 10, 15, 15)),
                        new TestOperationResult("expectedSource", "resultType1", "734", new DateTime(2019, 12, 4, 10, 15, 18)),
                    },
                    Option.None<EventHubSpecificReportComponents>(),
                    Option.None<DateTime>()));

            Assert.StartsWith("expectedSource", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenActualSourceIsNotProvided(string actualSource)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new CountingReport(
                    TestDescription,
                    "trackingId-23434",
                    "expectedSource",
                    actualSource,
                    "resultType1",
                    945,
                    923,
                    33,
                    new List<TestOperationResult>
                    {
                        new TestOperationResult("expectedSource", "resultType1", "332", new DateTime(2019, 12, 4, 10, 15, 15)),
                        new TestOperationResult("expectedSource", "resultType1", "734", new DateTime(2019, 12, 4, 10, 15, 18)),
                    },
                    Option.None<EventHubSpecificReportComponents>(),
                    Option.None<DateTime>()));

            Assert.StartsWith("actualSource", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenResultTypeIsNotProvided(string resultType)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new CountingReport(
                    TestDescription,
                    "trackingId-23434",
                    "expectedSource",
                    "actualSource",
                    resultType,
                    945,
                    923,
                    33,
                    new List<TestOperationResult>
                    {
                        new TestOperationResult("expectedSource", "resultType1", "332", new DateTime(2019, 12, 4, 10, 15, 15)),
                        new TestOperationResult("expectedSource", "resultType1", "734", new DateTime(2019, 12, 4, 10, 15, 18)),
                    },
                    Option.None<EventHubSpecificReportComponents>(),
                    Option.None<DateTime>()));

            Assert.StartsWith("resultType", ex.Message);
        }
    }
}
