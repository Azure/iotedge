// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Unit]
    public class JsonExTest
    {
        [Fact]
        public void TestStripMetadata()
        {
            // Arrange
            JToken input = JToken.FromObject(new Dictionary<string, object>
            {
                { "foo", 10 },
                { "bar", 20 },
                { "$metadata", new { baz = 30 } },
                { "$version", 40 }
            });

            // Act
            JsonEx.StripMetadata(input);

            // Assert
            JToken expected = JToken.FromObject(new
            {
                foo = 10,
                bar = 20
            });

            Assert.True(JToken.DeepEquals(expected, input));
        }

        [Fact]
        public void TestStripMetadata2()
        {
            // Arrange
            JToken input = JToken.FromObject(new Dictionary<string, object>
            {
                { "foo", 10 },
                { "bar", 20 },
                { "$metadata", new { baz = 30 } },
                { "$version", 40 },
                { "dontStripThis", new Dictionary<string, object>
                    {
                        { "$metadata", new { baz = 30 } },
                        { "$version", 40 }
                    }
                }
            });

            // Act
            JsonEx.StripMetadata(input);

            // Assert
            JToken expected = JToken.FromObject(new
            {
                foo = 10,
                bar = 20,
                dontStripThis = new Dictionary<string, object>
                {
                    { "$metadata", new { baz = 30 } },
                    { "$version", 40 }
                }
            });

            Assert.True(JToken.DeepEquals(expected, input));
        }

        [Fact]
        public void TestMergeAllCases()
        {
            // Arrange
            var baseline = new
            {
                name = new
                {
                    level0 = "nochange",
                    level1 = "value1",
                    level2 = new
                    {
                        level3 = "value3"
                    },
                    level6 = (Dictionary<string, string>)null,
                },
                overwrite = new
                {
                    level1 = "value1"
                },
                create = "yes"
            };

            var patch = new
            {
                name = new
                {
                    level0 = "nochange", // unchanged
                    level1 = (Dictionary<string, string>)null, // existing in base. remove property, only if treatNullAsDelete = true
                    level2 = new
                    {
                        level3 = "newvalue3" // existing in base, update property
                    },
                    level4 = "value4", // non existant in base, add new property
                    level5 = (Dictionary<string, string>)null // ignore, unless treatNullAsDelete = false
                },
                overwrite = "yes", // overwrite object with value
                create = new // overwrite value with object
                {
                    level1 = "value1",
                },
            };

            var removeAll = new
            {
                name = (Dictionary<string, string>)null,
                overwrite = (Dictionary<string, string>)null,
                create = (Dictionary<string, string>)null
            };

            var removeAllInefficient = new
            {
                name = new
                {
                    level0 = (Dictionary<string, string>)null,
                    level1 = (Dictionary<string, string>)null,
                    level2 = new
                    {
                        level3 = (Dictionary<string, string>)null,
                    },
                    level6 = (Dictionary<string, string>)null,
                },
                overwrite = new
                {
                    level1 = (Dictionary<string, string>)null,
                },
                create = (Dictionary<string, string>)null,
            };

            var mergedExcludeNull = new
            {
                name = new
                {
                    level0 = "nochange",
                    level2 = new
                    {
                        level3 = "newvalue3"
                    },
                    level4 = "value4",
                    level6 = (Dictionary<string, string>)null
                },
                overwrite = "yes",
                create = new
                {
                    level1 = "value1",
                }
            };

            var mergedIncludeNull = new
            {
                name = new
                {
                    level0 = "nochange",
                    level1 = (Dictionary<string, string>)null,
                    level2 = new
                    {
                        level3 = "newvalue3"
                    },
                    level4 = "value4",
                    level5 = (Dictionary<string, string>)null,
                    level6 = (Dictionary<string, string>)null
                },
                overwrite = "yes",
                create = new
                {
                    level1 = "value1",
                }
            };

            var emptyBaseline = new { };

            var nestedEmptyBaseline = new
            {
                name = new
                {
                    level2 = new
                    {
                    },
                },
                overwrite = new
                {
                },
            };

            var emptyPatch = new { };

            // Act
            JToken resultCollection = JsonEx.Merge(JToken.FromObject(baseline), JToken.FromObject(patch), true);

            // Assert
            Assert.True(JToken.DeepEquals(JToken.FromObject(resultCollection), JToken.FromObject(mergedExcludeNull)));

            // Act
            resultCollection = JsonEx.Merge(JToken.FromObject(baseline), JToken.FromObject(patch), false);

            // Assert
            Assert.True(JToken.DeepEquals(resultCollection, JToken.FromObject(mergedIncludeNull)));

            // Act
            resultCollection = JsonEx.Merge(JToken.FromObject(emptyBaseline), JToken.FromObject(emptyPatch), true);

            // Assert
            Assert.True(JToken.DeepEquals(resultCollection, JToken.FromObject(emptyBaseline)));

            // Act
            resultCollection = JsonEx.Merge(JToken.FromObject(baseline), JToken.FromObject(emptyPatch), true);

            // Assert
            Assert.True(JToken.DeepEquals(resultCollection, JToken.FromObject(baseline)));

            // Act
            resultCollection = JsonEx.Merge(JToken.FromObject(emptyBaseline), JToken.FromObject(patch), true);

            // Assert
            Assert.True(JToken.DeepEquals(resultCollection, JToken.FromObject(patch)));

            // Act
            resultCollection = JsonEx.Merge(JToken.FromObject(baseline), JToken.FromObject(removeAll), true);

            // Assert
            Assert.True(JToken.DeepEquals(resultCollection, JToken.FromObject(emptyBaseline)));

            // Act
            resultCollection = JsonEx.Merge(JToken.FromObject(baseline), JToken.FromObject(removeAllInefficient), true);

            // Assert
            Assert.True(JToken.DeepEquals(resultCollection, JToken.FromObject(nestedEmptyBaseline)));
        }

        [Fact]
        public void TestDiffAllCases()
        {
            // Arrange
            var baseline = new
            {
                name = new
                {
                    level0 = "nochange",
                    level1 = "value1",
                    level2 = new
                    {
                        level3 = "value3"
                    },
                    level6 = (Dictionary<string, string>)null,
                },
                overwrite = new
                {
                    level1 = "value1"
                },
                create = "yes"
            };

            var patch = new
            {
                name = new
                {
                    //["level0"] = "nochange", // unchanged
                    level1 = (Dictionary<string, string>)null, // existing in base. remove property
                    level2 = new
                    {
                        level3 = "newvalue3" // existing in base, update property
                    },
                    level4 = "value4", // non existant in base, add new property
                },
                overwrite = "yes", // overwrite object with value
                create = new // overwrite value with object
                {
                    level1 = "value1",
                },
            };

            var mergedExcludeNull = new
            {
                name = new
                {
                    level0 = "nochange", // unchanged
                    level2 = new
                    {
                        level3 = "newvalue3"
                    },
                    level4 = "value4",
                    level6 = (Dictionary<string, string>)null,
                },
                overwrite = "yes",
                create = new
                {
                    level1 = "value1",
                }
            };

            var removeAll = new
            {
                name = (Dictionary<string, string>)null,
                overwrite = (Dictionary<string, string>)null,
                create = (Dictionary<string, string>)null
            };

            var emptyBaseline = new { };

            var emptyPatch = new { };

            // Act
            JToken resultCollection = JsonEx.Diff(JToken.FromObject(baseline), JToken.FromObject(mergedExcludeNull));

            // Assert
            Assert.True(JToken.DeepEquals(resultCollection, JToken.FromObject(patch)));

            // Act
            resultCollection = JsonEx.Diff(JToken.FromObject(baseline), JToken.FromObject(baseline));

            // Assert
            Assert.True(JToken.DeepEquals(resultCollection, JToken.FromObject(emptyPatch)));

            // Act
            resultCollection = JsonEx.Diff(JToken.FromObject(emptyBaseline), JToken.FromObject(emptyBaseline));

            // Assert
            Assert.True(JToken.DeepEquals(resultCollection, JToken.FromObject(emptyPatch)));

            // Act
            resultCollection = JsonEx.Diff(JToken.FromObject(emptyBaseline), JToken.FromObject(mergedExcludeNull));

            // Assert
            Assert.True(JToken.DeepEquals(resultCollection, JToken.FromObject(mergedExcludeNull)));

            // Act
            resultCollection = JsonEx.Diff(JToken.FromObject(baseline), JToken.FromObject(emptyBaseline));

            // Assert
            Assert.True(JToken.DeepEquals(resultCollection, JToken.FromObject(removeAll)));
        }

        [Theory]
        [InlineData("a", "{}", "")]
        [InlineData("a", @"{'a':'1'}", "1")]
        [InlineData("a", @"{'a':'1','a01':'2'}", "12")]
        [InlineData("a", @"{'a01':'1','a':'2'}", "21")]
        public void ChunkedPropertyTest(string field, string input, string expected)
        {
            var obj = JObject.Parse(input);
            var chunked = obj.ChunkedValue(field, false).Select(t => t.ToString()).Join("");
            Assert.Equal(expected, chunked);
        }

        [Theory]
        [InlineData("a", @"{'a01':'1','A':'2'}", "21")]
        [InlineData("A", @"{'a01':'1','A':'2'}", "21")]
        public void ChunkedPropertyIgnoreCaseTest(string field, string input, string expected)
        {
            var obj = JObject.Parse(input);
            var chunked = obj.ChunkedValue(field, true).Select(t => t.ToString()).Join("");
            Assert.Equal(expected, chunked);
        }

        [Theory]
        [InlineData("a", @"{'a01':'1'}")]
        [InlineData("a", @"{'a':'1','a02':'2'}")]
        public void ChunkedPropertyThrows(string field, string input)
        {
            var obj = JObject.Parse(input);
            Assert.Throws<JsonSerializationException>(() => obj.ChunkedValue(field).Select(t => t.ToString()).Join());
        }
    }
}
