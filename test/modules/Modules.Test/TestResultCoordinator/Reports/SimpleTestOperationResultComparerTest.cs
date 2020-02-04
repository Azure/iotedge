// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using global::TestResultCoordinator.Reports;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
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
        public void TestMatches(string source1, string resultType1, string value1, string source2, string resultType2, string value2, bool isMatched)
        {
            var expected = new TestOperationResult(source1, resultType1, value1, DateTime.UtcNow);
            var actual = new TestOperationResult(source2, resultType2, value2, DateTime.UtcNow);

            Assert.Equal(isMatched, new SimpleTestOperationResultComparer().Matches(expected, actual));
        }
    }
}
