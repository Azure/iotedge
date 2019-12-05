using System;
using System.Collections.Generic;
using System.Text;

namespace DevOpsLibTest
{
    using DevOpsLib;
    using NUnit.Framework;

    [TestFixture]
    public class BuildExtensionTest
    {
        [Test]
        public void TestMasterBranchReporting()
        {
            HashSet<BuildDefinitionId> ids = BuildExtension.MasterBranchReporting;

            Assert.AreEqual(6, ids.Count);
            Assert.True(ids.Contains(BuildDefinitionId.CI));
            Assert.True(ids.Contains(BuildDefinitionId.EdgeletCI));
            Assert.True(ids.Contains(BuildDefinitionId.LibiohsmCI));
            Assert.True(ids.Contains(BuildDefinitionId.BuildImages));
            Assert.True(ids.Contains(BuildDefinitionId.EdgeletPackages));
            Assert.True(ids.Contains(BuildDefinitionId.EndToEndTest));
        }

        [Test]
        public void TestDisplayName()
        {
            Assert.AreEqual("Build Images", BuildDefinitionId.BuildImages.DisplayName());
            Assert.AreEqual("CI", BuildDefinitionId.CI.DisplayName());
            Assert.AreEqual("Edgelet CI", BuildDefinitionId.EdgeletCI.DisplayName());
            Assert.AreEqual("Edgelet Packages", BuildDefinitionId.EdgeletPackages.DisplayName());
            Assert.AreEqual("Edgelet Release", BuildDefinitionId.EdgeletRelease.DisplayName());
            Assert.AreEqual("End-to-End Test", BuildDefinitionId.EndToEndTest.DisplayName());
            Assert.AreEqual("Image Release", BuildDefinitionId.ImageRelease.DisplayName());
            Assert.AreEqual("Libiohsm CI", BuildDefinitionId.LibiohsmCI.DisplayName());
        }

        [Test]
        public void TestIdString()
        {
            Assert.AreEqual("45137", BuildDefinitionId.CI.IdString());
            Assert.AreEqual("39853", BuildDefinitionId.LibiohsmCI.IdString());
        }
    }
}
