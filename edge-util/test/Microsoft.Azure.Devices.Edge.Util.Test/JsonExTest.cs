// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public class JsonExTest
    {
        [Fact]
        [Unit]
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
        [Unit]
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
    }
}
