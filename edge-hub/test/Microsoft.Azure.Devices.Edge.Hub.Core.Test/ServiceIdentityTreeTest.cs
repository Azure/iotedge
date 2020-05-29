// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class ServiceIdentityTreeTest
    {
        // Test data hierarchy
        //            root
        //             /\
        //            /  \
        //           /    \
        //          /      \
        //      e1_L1      e2_L1
        //       /\          /\
        //      /  \        /  \
        //     /    \      /    \
        //  e1_L2 e2_L2 e3_L2  e4_L2
        //    |      |     |     |
        //  leaf1  mod1  leaf2  mod2
        readonly ServiceIdentity root = CreateServiceIdentity("root", null, "rootScope", null, true);
        readonly ServiceIdentity e1_L1 = CreateServiceIdentity("e1_L1", null, "e1_L1_scope", "rootScope", true);
        readonly ServiceIdentity e2_L1 = CreateServiceIdentity("e2_L1", null, "e2_L1_scope", "rootScope", true);
        readonly ServiceIdentity e1_L2 = CreateServiceIdentity("e1_L2", null, "e1_L2_scope", "e1_L1_scope", true);
        readonly ServiceIdentity e2_L2 = CreateServiceIdentity("e2_L2", null, "e2_L2_scope", "e1_L1_scope", true);
        readonly ServiceIdentity e3_L2 = CreateServiceIdentity("e3_L2", null, "e3_L2_scope", "e2_L1_scope", true);
        readonly ServiceIdentity e4_L2 = CreateServiceIdentity("e4_L2", null, "e4_L2_scope", "e2_L1_scope", true);
        readonly ServiceIdentity leaf1 = CreateServiceIdentity("leaf1", null, null, "e1_L2_scope", false);
        readonly ServiceIdentity leaf2 = CreateServiceIdentity("leaf2", null, null, "e3_L2_scope", false);
        readonly ServiceIdentity mod1 = CreateServiceIdentity("e2_L2", "mod1", null, null, false);
        readonly ServiceIdentity mod2 = CreateServiceIdentity("e4_L2", "mod2", null, null, false);

        internal static ServiceIdentity CreateServiceIdentity(string deviceId, string moduleId, string deviceScope, string parentScope, bool isEdge)
        {
            List<string> capabilities = new List<string>();

            if (isEdge)
            {
                capabilities.Add(Constants.IotEdgeIdentityCapability);
            }

            return new ServiceIdentity(
                deviceId,
                moduleId,
                deviceScope,
                parentScope == null ? new List<string>() : new List<string>() { parentScope },
                "1234",
                capabilities,
                new ServiceAuthentication(ServiceAuthenticationType.None),
                ServiceIdentityStatus.Enabled);
        }

        internal ServiceIdentityTree SetupTree()
        {
            var tree = new ServiceIdentityTree(this.root.Id);
            tree.InsertOrUpdate(this.root).Wait();
            tree.InsertOrUpdate(this.e1_L1).Wait();
            tree.InsertOrUpdate(this.e2_L1).Wait();
            tree.InsertOrUpdate(this.e1_L2).Wait();
            tree.InsertOrUpdate(this.e2_L2).Wait();
            tree.InsertOrUpdate(this.e3_L2).Wait();
            tree.InsertOrUpdate(this.e4_L2).Wait();
            tree.InsertOrUpdate(this.leaf1).Wait();
            tree.InsertOrUpdate(this.leaf2).Wait();
            tree.InsertOrUpdate(this.mod1).Wait();
            tree.InsertOrUpdate(this.mod2).Wait();

            return tree;
        }

        internal void CheckValidAuthChains(ServiceIdentityTree tree)
        {
            // Check leaf1
            Option<string> authChainActual = tree.GetAuthChain(this.leaf1.Id).Result;
            string leaf1_authchain_expected =
                this.leaf1.Id + ";" +
                this.e1_L2.Id + ";" +
                this.e1_L1.Id + ";" +
                this.root.Id;
            Assert.True(authChainActual.Contains(leaf1_authchain_expected));

            // Check leaf2
            authChainActual = tree.GetAuthChain(this.leaf2.Id).Result;
            string leaf2_authchain_expected =
                this.leaf2.Id + ";" +
                this.e3_L2.Id + ";" +
                this.e2_L1.Id + ";" +
                this.root.Id;
            Assert.True(authChainActual.Contains(leaf2_authchain_expected));

            // Check mod1
            authChainActual = tree.GetAuthChain(this.mod1.Id).Result;
            string mod1_authchain_expected =
                this.mod1.Id + ";" +
                this.e2_L2.Id + ";" +
                this.e1_L1.Id + ";" +
                this.root.Id;
            Assert.True(authChainActual.Contains(mod1_authchain_expected));

            // Check mod2
            authChainActual = tree.GetAuthChain(this.mod2.Id).Result;
            string mod2_authchain_expected =
                this.mod2.Id + ";" +
                this.e4_L2.Id + ";" +
                this.e2_L1.Id + ";" +
                this.root.Id;
            Assert.True(authChainActual.Contains(mod2_authchain_expected));
        }

        [Fact]
        public void GetAuthChain_Test()
        {
            // Setup our tree
            ServiceIdentityTree tree = this.SetupTree();

            // Check for valid auth chains
            this.CheckValidAuthChains(tree);

            // Check non-existent auth chain
            Assert.False(tree.GetAuthChain("nonexistent").Result.HasValue);

            // Insert an orphaned node and check for its invalid auth chain
            ServiceIdentity orphan = CreateServiceIdentity("orphan", null, null, null, false);
            tree.InsertOrUpdate(orphan).Wait();
            Assert.False(tree.GetAuthChain(orphan.Id).Result.HasValue);
        }

        [Fact]
        public void Insertion_OutOfOrder_Test()
        {
            var tree = new ServiceIdentityTree(this.root.Id);

            // Insert L2 identities
            tree.InsertOrUpdate(this.e1_L2).Wait();
            tree.InsertOrUpdate(this.e2_L2).Wait();
            tree.InsertOrUpdate(this.e3_L2).Wait();
            tree.InsertOrUpdate(this.e4_L2).Wait();

            // Should have no valid auth chains
            Assert.False(tree.GetAuthChain(this.e1_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e2_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e3_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e4_L2.Id).Result.HasValue);

            // Insert L1 identities
            tree.InsertOrUpdate(this.e1_L1).Wait();
            tree.InsertOrUpdate(this.e2_L1).Wait();

            // Should have no valid auth chains
            Assert.False(tree.GetAuthChain(this.e1_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e2_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e3_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e4_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e1_L1.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e2_L1.Id).Result.HasValue);

            // Insert leaf identities
            tree.InsertOrUpdate(this.leaf1).Wait();
            tree.InsertOrUpdate(this.leaf2).Wait();
            tree.InsertOrUpdate(this.mod1).Wait();
            tree.InsertOrUpdate(this.mod2).Wait();

            // Should have no valid auth chains
            Assert.False(tree.GetAuthChain(this.e1_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e2_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e3_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e4_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e1_L1.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e2_L1.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.leaf1.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.leaf2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.mod1.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.mod2.Id).Result.HasValue);

            // Insert root
            tree.InsertOrUpdate(this.root).Wait();

            // All auth chains should now be valid because root is available
            this.CheckValidAuthChains(tree);
        }

        [Fact]
        public void Update_Test()
        {
            ServiceIdentityTree tree = this.SetupTree();

            // Re-parent e3_L2 from e2_L1 to e1_L1
            ServiceIdentity updatedIdentity = CreateServiceIdentity(
                this.e3_L2.DeviceId,
                null,
                this.e3_L2.DeviceScope.Expect(() => new InvalidOperationException()),
                this.e1_L1.DeviceScope.Expect(() => new InvalidOperationException()),
                true);

            tree.InsertOrUpdate(updatedIdentity).Wait();

            // Equality check
            Option<ServiceIdentity> roundTripIdentity = tree.Get(updatedIdentity.Id).Result;
            Assert.True(roundTripIdentity.Contains(updatedIdentity));

            // The child of e3_L2, leaf2, should also go through a different path for authchain now
            Option<string> authChainActual = tree.GetAuthChain(this.leaf2.Id).Result;
            string leaf2_authchain_expected =
                this.leaf2.Id + ";" +
                this.e3_L2.Id + ";" +
                this.e1_L1.Id + ";" +
                this.root.Id;
            Assert.True(authChainActual.Contains(leaf2_authchain_expected));
        }

        [Fact]
        public void Remove_Test()
        {
            ServiceIdentityTree tree = this.SetupTree();

            // Delete a node
            tree.Remove(this.e2_L1.Id).Wait();

            // Auth-chains for everything in its subtree should be invalidated
            Assert.False(tree.GetAuthChain(this.e2_L1.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e3_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.e4_L2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.leaf2.Id).Result.HasValue);
            Assert.False(tree.GetAuthChain(this.mod2.Id).Result.HasValue);

            // Delete the rest of the subtree
            tree.Remove(this.e3_L2.Id).Wait();
            tree.Remove(this.e4_L2.Id).Wait();
            tree.Remove(this.leaf2.Id).Wait();
            tree.Remove(this.mod2.Id).Wait();

            // Nothing under e2_L1 should remain
            Assert.False(tree.Get(this.e2_L1.Id).Result.HasValue);
            Assert.False(tree.Get(this.e3_L2.Id).Result.HasValue);
            Assert.False(tree.Get(this.e4_L2.Id).Result.HasValue);
            Assert.False(tree.Get(this.leaf2.Id).Result.HasValue);
            Assert.False(tree.Get(this.mod2.Id).Result.HasValue);
        }
    }
}
