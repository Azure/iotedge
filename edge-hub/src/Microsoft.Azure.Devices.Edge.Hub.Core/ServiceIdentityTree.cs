// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    /// <summary>
    /// This is a tree implementation that uses the ParentScopes property
    /// to reconstruct the hierarchy of modules/devices under the current
    /// Edge device in a nested Edge environment.
    /// </summary>
    public class ServiceIdentityTree :
        IServiceIdentityTree,
        IAuthenticationChainProvider
    {
        readonly string rootDeviceId;
        AtomicReference<ImmutableDictionary<string, ServiceIdentityTreeNode>> nodes;

        public ServiceIdentityTree(string rootDeviceId)
        {
            this.rootDeviceId = Preconditions.CheckNonWhiteSpace(rootDeviceId, nameof(rootDeviceId));
            this.nodes = new AtomicReference<ImmutableDictionary<string, ServiceIdentityTreeNode>>(ImmutableDictionary<string, ServiceIdentityTreeNode>.Empty);
        }

        public void InsertOrUpdate(ServiceIdentity identity)
        {
            // There should always be a valid ServiceIdentity
            Preconditions.CheckNotNull(identity, nameof(identity));

            ImmutableDictionary<string, ServiceIdentityTreeNode> snapshot = this.nodes;
            if (snapshot.ContainsKey(identity.Id))
            {
                // Update case - this is just remove + re-insert
                this.Remove(identity.Id);

                // Take another snapshot since an item was removed
                snapshot = this.nodes;
            }

            var writableSnapshot = new Dictionary<string, ServiceIdentityTreeNode>(snapshot);

            // Insert case
            if (identity.IsModule)
            {
                this.InsertModuleIdentity(writableSnapshot, identity);
            }
            else
            {
                this.InsertDeviceIdentity(writableSnapshot, identity);
            }

            // Write back to the atomic field
            if (!this.nodes.CompareAndSet(snapshot, writableSnapshot.ToImmutableDictionary()))
            {
                // There's only ever one thread calling InsertOrUpdate, so this should never fail
                throw new InvalidOperationException("Multi-threaded insert/update detected on ServiceIdentityTree");
            }
        }

        public void Remove(string id)
        {
            ImmutableDictionary<string, ServiceIdentityTreeNode> snapshot = this.nodes;
            var writableSnapshot = new Dictionary<string, ServiceIdentityTreeNode>(snapshot);

            if (writableSnapshot.TryGetValue(id, out ServiceIdentityTreeNode target))
            {
                // Unhook from parent
                target.Parent.ForEach(p => p.RemoveChild(target));

                // Unhook the children
                target.RemoveAllChildren();

                // Remove the node itself
                writableSnapshot.Remove(id);
            }

            // Write back to the atomic field
            if (!this.nodes.CompareAndSet(snapshot, writableSnapshot.ToImmutableDictionary()))
            {
                // There's only ever one thread calling Remove(), so this should never fail
                throw new InvalidOperationException("Multi-threaded delete detected on ServiceIdentityTree");
            }
        }

        public bool Contains(string id)
        {
            ImmutableDictionary<string, ServiceIdentityTreeNode> snapshot = this.nodes;
            return snapshot.ContainsKey(id);
        }

        public Option<ServiceIdentity> Get(string id)
        {
            ImmutableDictionary<string, ServiceIdentityTreeNode> snapshot = this.nodes;
            return snapshot.TryGetValue(id, out ServiceIdentityTreeNode treeNode)
                    ? Option.Some(treeNode.Identity)
                    : Option.None<ServiceIdentity>();
        }

        public IList<string> GetAllIds()
        {
            ImmutableDictionary<string, ServiceIdentityTreeNode> snapshot = this.nodes;
            return snapshot.Select(kvp => kvp.Value.Identity.Id).ToList();
        }

        public bool TryGetAuthChain(string id, out string authChain)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            authChain = string.Empty;

            // Auth-chain for the root (when there aren't any nodes in the tree yet)
            if (id == this.rootDeviceId)
            {
                authChain = this.rootDeviceId;
                return true;
            }

            // Auth-chain for a child somewhere in the tree
            ImmutableDictionary<string, ServiceIdentityTreeNode> snapshot = this.nodes;
            if (snapshot.TryGetValue(id, out ServiceIdentityTreeNode treeNode) &&
                treeNode.AuthChain.HasValue)
            {
                // Auth chain starts with the originating device
                authChain = treeNode.AuthChain.Expect(() => new InvalidOperationException());
                return true;
            }

            return false;
        }

        void InsertModuleIdentity(IDictionary<string, ServiceIdentityTreeNode> nodes, ServiceIdentity module)
        {
            var newNode = new ServiceIdentityTreeNode(module, Option.None<string>());
            nodes.Add(module.Id, newNode);

            if (nodes.TryGetValue(module.DeviceId, out ServiceIdentityTreeNode parentDeviceNode))
            {
                // Hook up the module to the parent device
                parentDeviceNode.AddChild(newNode);
            }
            else
            {
                // It's okay if we can't find the parent device, it's possible that the identity
                // for the parent device hasn't came in yet. In this case we'll just leave the
                // module as a dangling node, and the parent should come in at a later point.
            }
        }

        void InsertDeviceIdentity(IDictionary<string, ServiceIdentityTreeNode> nodes, ServiceIdentity device)
        {
            // Root device is the base-case for constructing the authchain,
            // child devices derive their authchain from the parent
            var newNode = device.Id == this.rootDeviceId ?
                new ServiceIdentityTreeNode(device, Option.Some(this.rootDeviceId)) :
                new ServiceIdentityTreeNode(device, Option.None<string>());

            nodes.Add(device.Id, newNode);

            // Check if there's an existing parent for this device
            foreach (string parentScopeId in device.ParentScopes)
            {
                // Look for the parent based on scope ID, it's okay if we can't find a parent
                // node. It's possible that the identity for the parent hasn't came in yet.
                // In this case we'll just leave the new identity as a dangling node, and
                // the parent device identity should come in at a later point.
                Option<ServiceIdentityTreeNode> parentNode = this.FindDeviceByScopeId(nodes, parentScopeId);

                // Hook up the new node into the tree
                parentNode.ForEach(p =>
                {
                    p.AddChild(newNode);
                });
            }

            // Check if there are any dangling children that can now be hooked up,
            // this include placing the new node as the new root.
            List<ServiceIdentityTreeNode> danglingChildren =
                nodes
                .Select(kvp => kvp.Value)
                .Where(s => s.Identity.ParentScopes.Count() > 0 && s.Identity.ParentScopes.Contains(device.DeviceScope.OrDefault()))
                .ToList();

            foreach (ServiceIdentityTreeNode child in danglingChildren)
            {
                newNode.AddChild(child);
            }
        }

        Option<ServiceIdentityTreeNode> FindDeviceByScopeId(IDictionary<string, ServiceIdentityTreeNode> nodes, string scopeId)
        {
            Preconditions.CheckNonWhiteSpace(scopeId, nameof(scopeId));

            // Look for Edge devices with a matching scope ID
            List<ServiceIdentityTreeNode> devices =
                nodes
                .Select(kvp => kvp.Value)
                .Where(serviceIdentity => serviceIdentity.Identity.IsEdgeDevice && serviceIdentity.Identity.DeviceScope.Contains(scopeId))
                .ToList();

            if (devices.Count() > 0)
            {
                // There shouldn't be more than one device, but even if there is
                // for some reason, we can just use the first one
                return Option.Some(devices.First());
            }

            return Option.None<ServiceIdentityTreeNode>();
        }

        /// <summary>
        /// This is an object to wrap ServiceIdentity so that it can
        /// point to a parent and act as a node in a tree.
        /// </summary>
        internal class ServiceIdentityTreeNode
        {
            public ServiceIdentity Identity { get; }
            public Option<string> AuthChain { get; private set; }
            public Option<ServiceIdentityTreeNode> Parent { get; private set; }
            List<ServiceIdentityTreeNode> children;

            public ServiceIdentityTreeNode(ServiceIdentity identity, Option<string> authChain)
            {
                this.Identity = Preconditions.CheckNotNull(identity);
                this.Parent = Option.None<ServiceIdentityTreeNode>();
                this.children = new List<ServiceIdentityTreeNode>();
                this.AuthChain = authChain;
            }

            public void AddChild(ServiceIdentityTreeNode childNode)
            {
                // TODO: Enforce tree depth
                this.children.Add(childNode);
                childNode.Parent = Option.Some(this);
                childNode.UpdateAuthChainFromParent(this);
            }

            public void RemoveChild(ServiceIdentityTreeNode childNode)
            {
                this.children.Remove(childNode);
                childNode.Parent = Option.None<ServiceIdentityTreeNode>();
                childNode.RemoveAuthChain();
            }

            public void RemoveAllChildren()
            {
                var snapshot = new List<ServiceIdentityTreeNode>(this.children);
                snapshot.ForEach(child => this.RemoveChild(child));
            }

            void UpdateAuthChainFromParent(ServiceIdentityTreeNode parentNode)
            {
                // Our auth chain is inherited from the parent's chain
                this.AuthChain = parentNode.AuthChain.Map(parentChain => this.MakeAuthChainFromParent(parentChain));

                // Recurisvely update all children to re-calculate their authchains
                this.children.ForEach(child => child.UpdateAuthChainFromParent(this));
            }

            void RemoveAuthChain()
            {
                this.AuthChain = Option.None<string>();

                // Recursively remove the authchains of children
                this.children.ForEach(child => child.RemoveAuthChain());
            }

            private string MakeAuthChainFromParent(string parentAuthChain)
            {
                return this.Identity.Id + ";" + parentAuthChain;
            }
        }
    }
}
