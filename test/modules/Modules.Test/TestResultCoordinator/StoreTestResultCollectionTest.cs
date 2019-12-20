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

    public class StoreTestResultCollectionTest
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
        public async Task SimpleResultsEnumeration(
            string[] expectedStoreValues,
            int batchSize)
        {
            string expectedSource = "expectedSource";
            string resultType = "resultType1";

            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            var expectedResults = new StoreTestResultCollection<TestOperationResult>(mockExpectedStore.Object, batchSize);

            var expectedStoreData = GetStoreData(expectedSource, resultType, expectedStoreValues);
            for (int i = 0; i < expectedStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockExpectedStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(expectedStoreData.Skip(startingOffset).Take(batchSize));
            }

            int j = 0;
            while (await expectedResults.MoveNextAsync())
            {
                Assert.Equal(expectedStoreValues[j], expectedResults.Current.Result);
                j++;
            }
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
