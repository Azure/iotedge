// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Twin
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Twin;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Xunit;

    public class ReportedPropertiesValidatorTest
    {
        public static IEnumerable<object[]> GetTwinCollections()
        {
            string longString = new string('*', 5000);

            yield return new object[]
            {
                new TwinCollection(JsonConvert.SerializeObject(new
                {
                    level1 = new
                    {
                        level2 = new
                        {
                            level3 = new
                            {
                                level4 = new
                                {
                                    level5 = new { }
                                }
                            }
                        }
                    }
                })),
                null
            };

            yield return new object[]
            {
                new TwinCollection(JsonConvert.SerializeObject(new
                {
                    ok = "ok",
                    level = new
                    {
                        ok = "ok",
                        s = longString
                    }
                })),
                typeof(InvalidOperationException)
            };

            yield return new object[]
            {
                new TwinCollection(JsonConvert.SerializeObject(new
                {
                    level = new
                    {
                        number = -4503599627370497
                    }
                })),
                typeof(InvalidOperationException)
            };

            yield return new object[]
            {
                new TwinCollection(JsonConvert.SerializeObject(new
                {
                    level1 = new
                    {
                        level2 = new
                        {
                            level3 = new
                            {
                                level4 = new
                                {
                                    level5 = new
                                    {
                                        level6 = new { }
                                    }
                                }
                            }
                        }
                    }
                })),
                typeof(InvalidOperationException)
            };

            yield return new object[]
            {
                new TwinCollection(JsonConvert.SerializeObject(new
                {
                    array = new[] { 0, 1, 2 }
                })),
                typeof(InvalidOperationException)
            };

            yield return new object[]
            {
                new TwinCollection(JsonConvert.SerializeObject(new
                {
                    tooBig = new byte[10 * 1024]
                })),
                typeof(InvalidOperationException)
            };
        }

        [Unit]
        [Theory]
        [MemberData(nameof(GetTwinCollections))]
        public void ValidateReportedPropertiesTest(TwinCollection twinCollection, Type expectedExceptionType)
        {
            // Arrange
            var reportedPropertiesValidator = new ReportedPropertiesValidator();

            // Act/Assert
            if (expectedExceptionType == null)
            {
                reportedPropertiesValidator.Validate(twinCollection);
            }
            else
            {
                Assert.Throws(expectedExceptionType, () => reportedPropertiesValidator.Validate(twinCollection));
            }
        }
    }
}
