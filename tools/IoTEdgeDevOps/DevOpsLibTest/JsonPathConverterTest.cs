using System;
using System.Collections.Generic;
using System.Text;

namespace DevOpsLibTest
{
    using DevOpsLib;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [TestFixture]
    public class JsonPathConverterTest
    {
        [Test]
        public void TestCanConvert()
        {
            Assert.False(new JsonPathConverter().CanConvert(typeof(object)));
        }

        [Test]
        public void TestCanWrite()
        {
            Assert.False(new JsonPathConverter().CanWrite);
        }

        [Test]
        public void TestWriteJson()
        {
            Assert.That(() => new JsonPathConverter().WriteJson(null, new object(), new JsonSerializer()), Throws.TypeOf<NotImplementedException>());
        }
    }
}
