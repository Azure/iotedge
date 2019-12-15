// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::TestResultCoordinator;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Moq;
    using Xunit;

    public class SimpleResultsTest
    {
        public static IEnumerable<object[]> GetCreateReportData =>
            new List<object[]>
            {
                new object[] { Enumerable.Range(1, 1000).Select(v => v.ToString()), 10 },
                new object[] { new[] { "1", "2", "3", "4", "5", "6" }, 10 },
                new object[] { new[] { "2", "3", "4", "5", "6", "7" }, 10 },
                new object[] { new[] { "1", "3", "4", "5", "6", "7" }, 10 },
                new object[] { new[] { "1", "3", "4", "6", "7" }, 10 },
                new object[] { new[] { "1", "3", "4", "5", "6" }, 10 },
                new object[] { new[] { "2", "3", "4", "5", "7" }, 10 },
                new object[] { new[] { "1", "2", "2", "3", "4", "4", "5", "6" }, 10 },
                new object[] { new[] { "1", "1", "2", "3", "4", "5", "6", "6" }, 10 },
                new object[] { new[] { "1", "2", "3", "4", "5", "6" }, 4 },
                new object[] { new[] { "2", "3", "4", "5", "6", "7" }, 4 },
                new object[] { new[] { "1", "3", "4", "5", "6", "7" }, 4 },
                new object[] { new[] { "1", "3", "4", "6", "7" }, 4 },
                new object[] { new[] { "1", "3", "4", "5", "6" }, 4 },
                new object[] { new[] { "2", "3", "4", "5", "7" }, 4 },
            };

        [Theory]
        [MemberData(nameof(GetCreateReportData))]
        public void SimpleResultsEnumeration(
            string[] expectedStoreValues,
            int batchSize)
        {
            string expectedSource = "expectedSource";
            string resultType = "resultType1";

            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            IEnumerable<TestOperationResult> expectedResults = new SimpleResults(expectedSource, mockExpectedStore.Object, batchSize);

            var expectedStoreData = GetStoreData(expectedSource, resultType, expectedStoreValues);
            for (int i = 0; i < expectedStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockExpectedStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(expectedStoreData.Skip(startingOffset).Take(batchSize));
            }

            IEnumerator<TestOperationResult> enumerator = expectedResults.GetEnumerator();
            int j = 0;
            while (enumerator.MoveNext())
            {
                Assert.Equal(expectedStoreValues[j], enumerator.Current.Result);
                j++;
            }

            Assert.Null(enumerator.Current);
        }

        static List<(long, TestOperationResult)> GetStoreData(string source, string resultType, IEnumerable<string> resultValues, int start = 0)
        {
            var storeData = new List<(long, TestOperationResult)>();
            int count = start;

            foreach (string value in resultValues)
            {
                storeData.Add((count, new TestOperationResult(source, resultType, value, DateTime.UtcNow)));
                count++;
            }

            return storeData;
        }
    }
}
