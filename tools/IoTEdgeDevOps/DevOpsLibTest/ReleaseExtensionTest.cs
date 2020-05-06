// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLibTest
{
    using DevOpsLib;
    using NUnit.Framework;

    [TestFixture]
    public class ReleaseExtensionTest
    {
        [Test]
        public void TestIdString()
        {
            Assert.AreEqual("2189", ReleaseDefinitionId.E2ETest.IdString());
        }
    }
}
