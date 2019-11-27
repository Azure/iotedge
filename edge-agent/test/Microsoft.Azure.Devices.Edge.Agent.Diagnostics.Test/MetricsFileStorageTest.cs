// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class MetricsFileStorageTest : IDisposable
    {
        DateTime fakeTime;
        ISystemTime systemTime;
        TempDirectory tempDirectory = new TempDirectory();

        public MetricsFileStorageTest()
        {
            var systemTime = new Mock<ISystemTime>();
            this.fakeTime = new DateTime(100000000);
            systemTime.Setup(x => x.UtcNow).Returns(() => this.fakeTime);
            this.systemTime = systemTime.Object;
        }

        [Fact]
        public void TestWriteData()
        {
            string directory = Path.Combine(this.tempDirectory.CreateTempDir(), "metricsStore");
            MetricsFileStorage storage = new MetricsFileStorage(directory, this.systemTime);

            storage.WriteData(string.Join(", ", Enumerable.Range(0, 10)));

            Assert.NotEmpty(Directory.GetFiles(directory));
        }

        [Fact]
        public void TestGetDataSingleEntry()
        {
            MetricsFileStorage storage = new MetricsFileStorage(this.tempDirectory.CreateTempDir(), this.systemTime);

            string testData = string.Join(", ", Enumerable.Range(0, 10));
            storage.WriteData(testData);

            IDictionary<DateTime, Func<string>> actual = storage.GetData();
            Assert.Single(actual);
            Assert.Equal(testData, actual.Single().Value());
        }

        [Fact]
        public void TestGetDataByTime()
        {
            MetricsFileStorage storage = new MetricsFileStorage(this.tempDirectory.CreateTempDir(), this.systemTime);

            // Write fake data
            storage.WriteData("data1");
            DateTime break12 = this.fakeTime.AddMinutes(5);
            this.fakeTime = this.fakeTime.AddMinutes(10);
            storage.WriteData("data2");
            DateTime break23 = this.fakeTime.AddMinutes(5);
            this.fakeTime = this.fakeTime.AddMinutes(10);
            storage.WriteData("data3");
            DateTime data3Time = this.fakeTime;
            this.fakeTime = this.fakeTime.AddMinutes(10);
            storage.WriteData("data4");
            DateTime data4Time = this.fakeTime;

            // Gets all data
            IDictionary<DateTime, Func<string>> actual = storage.GetData();
            Assert.Equal(new string[] { "data1", "data2", "data3", "data4" }, actual.Select(a => a.Value()).OrderBy(d => d));

            // Gets data newer than a date
            actual = storage.GetData(break12);
            Assert.Equal(new string[] { "data2", "data3", "data4" }, actual.Select(a => a.Value()).OrderBy(d => d));

            // Gets data only between a start and end date
            actual = storage.GetData(break12, break23);
            Assert.Equal(new string[] { "data2" }, actual.Select(a => a.Value()).OrderBy(d => d));

            // start time is inclusive
            actual = storage.GetData(data3Time);
            Assert.Equal(new string[] { "data3", "data4" }, actual.Select(a => a.Value()).OrderBy(d => d));

            // start and end time are inclusive
            actual = storage.GetData(data3Time, data4Time);
            Assert.Equal(new string[] { "data3", "data4" }, actual.Select(a => a.Value()).OrderBy(d => d));
        }

        [Fact]
        public void TestRemoveOldEntries()
        {
            MetricsFileStorage storage = new MetricsFileStorage(this.tempDirectory.CreateTempDir(), this.systemTime);

            storage.WriteData("data1");

            IDictionary<DateTime, Func<string>> actual = storage.GetData();
            Assert.Single(actual);
            Assert.Equal("data1", actual.Single().Value());

            DateTime break1 = this.fakeTime.AddMinutes(5);
            this.fakeTime = this.fakeTime.AddMinutes(10);
            storage.WriteData("data2");

            actual = storage.GetData();
            Assert.Equal(2, actual.Count);
            storage.RemoveOldEntries(break1);
            actual = storage.GetData();
            Assert.Single(actual);

            DateTime break2 = this.fakeTime.AddMinutes(5);
            this.fakeTime = this.fakeTime.AddMinutes(10);
            storage.WriteData("data3");
            this.fakeTime = this.fakeTime.AddMinutes(10);
            storage.WriteData("data4");
            this.fakeTime = this.fakeTime.AddMinutes(10);
            storage.WriteData("data5");
            this.fakeTime = this.fakeTime.AddMinutes(10);

            actual = storage.GetData();
            Assert.Equal(4, actual.Count);
            storage.RemoveOldEntries(break2);
            actual = storage.GetData();
            Assert.Equal(new[] { "data3", "data4", "data5" }, actual.OrderBy(x => x.Key).Select(x => x.Value()));

            storage.RemoveOldEntries(DateTime.UtcNow);
            actual = storage.GetData();
            Assert.Empty(actual);
        }

        public void Dispose()
        {
            this.tempDirectory.Dispose();
        }
    }
}
