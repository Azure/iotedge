// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class SchemaVersionHelperTest
    {
        public static IEnumerable<object[]> CompareToTestData()
        {
            yield return new object[] { new Version("1.0"), "1.0", 0 };

            yield return new object[] { new Version("1.4"), "1.0", 1 };

            yield return new object[] { new Version("1.4"), "1.8", -1 };

            yield return new object[] { new Version("2.4"), "2.10", -1 };
        }

        public static IEnumerable<object[]> CompareToTestDataWithInvalidVersion()
        {
            yield return new object[] { new Version("1.0"), string.Empty, typeof(InvalidSchemaVersionException) };

            yield return new object[] { new Version("1.4"), "a.b", typeof(InvalidSchemaVersionException) };

            yield return new object[] { new Version("1.0"), "2.0", typeof(InvalidSchemaVersionException) };
        }

        [Theory]
        [MemberData(nameof(CompareToTestData))]
        public void CompareToTest(Version expectedVersion, string version, int expectedResult)
        {
            int result = expectedVersion.CompareMajorVersion(version, "dummy");
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(CompareToTestDataWithInvalidVersion))]
        public void CompareToWithInvalidVersionTest(Version expectedVersion, string version, Type exception)
        {
            Assert.Throws(exception, () => expectedVersion.CompareMajorVersion(version, "dummy"));
        }
    }
}
