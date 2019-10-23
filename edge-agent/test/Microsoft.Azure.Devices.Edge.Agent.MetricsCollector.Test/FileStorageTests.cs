using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Azure.Devices.Edge.Util.Test.Common;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector.Test
{
    public class FileStorageTests : TempDirectory
    {
        private DateTime fakeTime;
        private ISystemTime systemTime;
        public FileStorageTests()
        {
            var systemTime = new Mock<ISystemTime>();
            fakeTime = new DateTime(100000000);
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);
            this.systemTime = systemTime.Object;
        }

        [Fact]
        public void Storage()
        {
            string directory = GetTempDir();
            FileStorage storage = new FileStorage(directory, systemTime);

            storage.AddScrapeResult(string.Join(", ", Enumerable.Range(0, 10)));

            Assert.NotEmpty(Directory.GetFiles(directory));
        }        

        [Fact]
        public void GetDataSingleEntry()
        {
            FileStorage storage = new FileStorage(GetTempDir(), systemTime);

            string testData = string.Join(", ", Enumerable.Range(0, 10));
            storage.AddScrapeResult(testData);

            IDictionary<DateTime, Func<string>> actual = storage.GetData();
            Assert.Single(actual);
            Assert.Equal(testData, actual.Single().Value());
        }

        [Fact]
        public void GetDataByTime()
        {
            FileStorage storage = new FileStorage(GetTempDir(), systemTime);

            storage.AddScrapeResult("data1");

            IDictionary<DateTime, Func<string>> actual = storage.GetData();
            Assert.Single(actual);
            Assert.Equal("data1", actual.Single().Value());

            DateTime break1 = fakeTime.AddMinutes(5);
            fakeTime = fakeTime.AddMinutes(10);
            storage.AddScrapeResult("data2");

            actual = storage.GetData();
            Assert.Equal(2, actual.Count);
            actual = storage.GetData(break1);
            Assert.Single(actual);
            Assert.Equal("data2", actual.Single().Value());

            DateTime break2 = fakeTime.AddMinutes(5);
            fakeTime = fakeTime.AddMinutes(10);
            storage.AddScrapeResult("data3");

            actual = storage.GetData();
            Assert.Equal(3, actual.Count);
            actual = storage.GetData(break1, break2);
            Assert.Single(actual);
            Assert.Equal("data2", actual.Single().Value());
        }

        [Fact]
        public void RemoveOld()
        {
            FileStorage storage = new FileStorage(GetTempDir(), systemTime);

            storage.AddScrapeResult("data1");

            IDictionary<DateTime, Func<string>> actual = storage.GetData();
            Assert.Single(actual);
            Assert.Equal("data1", actual.Single().Value());

            DateTime break1 = fakeTime.AddMinutes(5);
            fakeTime = fakeTime.AddMinutes(10);
            storage.AddScrapeResult("data2");

            actual = storage.GetData();
            Assert.Equal(2, actual.Count);
            storage.RemoveOldEntries(break1);
            actual = storage.GetData();
            Assert.Single(actual);

            DateTime break2 = fakeTime.AddMinutes(5);
            fakeTime = fakeTime.AddMinutes(10);
            storage.AddScrapeResult("data3");
            fakeTime = fakeTime.AddMinutes(10);
            storage.AddScrapeResult("data4");
            fakeTime = fakeTime.AddMinutes(10);
            storage.AddScrapeResult("data5");
            fakeTime = fakeTime.AddMinutes(10);

            actual = storage.GetData();
            Assert.Equal(4, actual.Count);
            storage.RemoveOldEntries(break2);
            actual = storage.GetData();
            Assert.Equal(new[] { "data3", "data4", "data5" }, actual.OrderBy(x => x.Key).Select(x => x.Value()));

            storage.RemoveOldEntries(DateTime.UtcNow);
            actual = storage.GetData();
            Assert.Empty(actual);
        }
    }
}
