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
                                    level5 = new
                                    {
                                        level6 = new
                                        {
                                            level7 = new
                                            {
                                                level8 = new
                                                {
                                                    level9 = new
                                                    {
                                                        level10 = new { }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                })),
                null,
                string.Empty
            };

            yield return new object[]
            {
                new TwinCollection(JsonConvert.SerializeObject(new
                {
                    ok = "ok",
                    level = new
                    {
                        ok = "ok",
                        propertyWithBigValue = longString
                    }
                })),
                typeof(InvalidOperationException),
                "Value associated with property name propertyWithBigValue has length 5000 that exceeds maximum length of 4096"
            };

            yield return new object[]
            {
                new TwinCollection(JsonConvert.SerializeObject(new
                {
                    level = new
                    {
                        invalidNumber = -4503599627370497
                    }
                })),
                typeof(InvalidOperationException),
                "Property invalidNumber has an out of bound value. Valid values are between -4503599627370496 and 4503599627370495"
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
                                        level6 = new
                                        {
                                            level7 = new
                                             {
                                                level8 = new
                                                {
                                                    level9 = new
                                                    {
                                                        level10 = new
                                                        {
                                                            level11 = new { }
                                                        }
                                                    }
                                                }
                                             }
                                        }
                                    }
                                }
                            }
                        }
                    }
                })),
                typeof(InvalidOperationException),
                "Nested depth of twin property exceeds 10"
            };

            yield return new object[]
            {
                new TwinCollection(JsonConvert.SerializeObject(new
                {
                    array = new[] { 0, 1, 2 }
                })),
                null,
                string.Empty
            };

            yield return new object[]
            {
                new TwinCollection(JsonConvert.SerializeObject(new
                {
                    tooBig = longString
                })),
                typeof(InvalidOperationException),
                "Value associated with property name tooBig has length 5000 that exceeds maximum length of 4096"
            };

            yield return new object[]
            {
                new TwinCollection(JsonConvert.SerializeObject(new
                {
                    ok = "ok",
                    level1 = new
                    {
                        ok = null as string
                    }
                })),
                null,
                string.Empty
            };

            yield return new object[]
            {
                new TwinCollection("{ \"ok\":\"good\", \"level1\": { \"field1\": null } }"),
                null,
                string.Empty
            };

            yield return new object[]
            {
                new TwinCollection("{ \"o#k\":\"good\", \"level1\": { \"field1\": null } }"),
                typeof(InvalidOperationException),
                "Property name o#k contains invalid character '#'"
            };

            yield return new object[]
           {
                new TwinCollection($"{{ \"{longString} \":\"good\", \"level1\":{{ \"field1\": null }} }}"),
                typeof(InvalidOperationException),
                "Length of property name **********.. exceeds maximum length of 1024"
           };

            yield return new object[]
            {
                new TwinCollection(JsonConvert.SerializeObject(new
                {
                    LargeByteArray = new byte[3000],
                    LargeByteArray2 = new byte[3000],
                    LargeByteArray3 = new byte[3000],
                    LargeByteArray4 = new byte[3000],
                    LargeByteArray5 = new byte[3000],
                    LargeByteArray6 = new byte[3000],
                    LargeByteArray7 = new byte[3000],
                    LargeByteArray8 = new byte[3000],
                    LargeByteArray9 = new byte[3000]
                } )),
                typeof(InvalidOperationException),
                "Twin properties size 36189 exceeds maximum 32768"
            };

            yield return new object[]
            {
                new TwinCollection("{ \"ok\": [\"good\"], \"ok2\": [], \"level1\": [{ \"field1\": null }] }"),
                null,
                string.Empty
            };
        }

        [Unit]
        [Theory]
        [MemberData(nameof(GetTwinCollections))]
        public void ValidateReportedPropertiesTest(TwinCollection twinCollection, Type expectedExceptionType, string expectedExceptionMessage)
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
                Exception ex = Assert.Throws(expectedExceptionType, () => reportedPropertiesValidator.Validate(twinCollection));
                Assert.Equal(expectedExceptionMessage, ex.Message);
            }
        }
    }
}
