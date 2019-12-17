// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using global::TestResultCoordinator;
    using global::TestResultCoordinator.Report;
    using Xunit;

    public class SimpleTestOperationResultComparerTest
    {
        public static IEnumerable<object[]> GetTestDataForMatches =>
            new List<object[]>
            {
                new object[] { "source1", "resultType1", "value1", "source2", "resultType1", "value1", true },
                new object[] { "source1", "resulTTypE1", "value1", "source2", "resultType1", "value1", true },
                new object[] { "source1", "resultType1", "VaLuE1", "source2", "resultType1", "value1", true },
                new object[] { "source1", "resultType1", "value1", "source2", "resultType1", "value2", false },
                new object[] { "source1", "resultType1", "value1", "source2", "resultType2", "value1", false },
            };

        [Theory]
        [MemberData(nameof(GetTestDataForMatches))]
        public void TestMatches(string source1, string resultType1, string value1, string source2, string resultType2, string value2, bool expected)
        {
            var result1 = new TestOperationResult(source1, resultType1, value1, DateTime.UtcNow);
            var result2 = new TestOperationResult(source2, resultType2, value2, DateTime.UtcNow);

            Assert.Equal(expected, new SimpleTestOperationResultComparer().Matches(result1, result2));
        }
    }
}
