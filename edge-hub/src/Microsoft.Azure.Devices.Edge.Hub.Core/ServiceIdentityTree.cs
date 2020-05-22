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
            var writableSnapshot = new Dictionary<string, ServiceIdentityTreeNode>(snapshot);
            if (snapshot.ContainsKey(identity.Id))
            {
                // Update case - this is just remove + re-insert
                this.Remove(identity.Id);
            }

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
                // Unhook from the parent
                target.Parent.ForEach(parent => parent.Children.Remove(target));

                // Unhook the children
                target.Children.ForEach(child => child.Parent = Option.None<ServiceIdentityTreeNode>());

                // Remove
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

            // Auth-chain for root
            if (this.rootDeviceId == id)
            {
                authChain = this.rootDeviceId;
                return true;
            }

            // Auth-chain for a child somewhere in the tree
            ImmutableDictionary<string, ServiceIdentityTreeNode> snapshot = this.nodes;
            if (snapshot.TryGetValue(id, out ServiceIdentityTreeNode treeNode))
            {
                // Auth chain starts with the originating device
                authChain = treeNode.Identity.Id;

                // Walk the parents up to the root
                while (treeNode.Parent.HasValue)
                {
                    // Accumulate the auth chain per parent visited in the format:
                    // "leaf1;edge1;edge2;...;rootEdge"
                    ServiceIdentityTreeNode parent = treeNode.Parent.Expect(() => new InvalidOperationException());
                    authChain += ";" + parent.Identity.Id;

                    if (parent.Identity.DeviceId == this.rootDeviceId)
                    {
                        // We've reached the root device, we're done
                        return true;
                    }

                    // Move on to the next parent
                    treeNode = parent;
                }

                // If we walked the parent chain up to a dead-end, i.e. topmost node does not
                // match tree root device ID, then it means this subtree is dangling.
                // This is a possible scenario, we just treat it as if there's no auth chain.
            }

            // No valid auth chain
            return false;
        }

        void InsertModuleIdentity(IDictionary<string, ServiceIdentityTreeNode> nodes, ServiceIdentity module)
        {
            var newNode = new ServiceIdentityTreeNode(module);
            nodes.Add(module.Id, newNode);

            if (nodes.TryGetValue(module.DeviceId, out ServiceIdentityTreeNode parentDeviceNode))
            {
                // Hook up the module to the parent device
                newNode.Parent = Option.Some(parentDeviceNode);
                parentDeviceNode.Children.Add(newNode);
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
            var newNode = new ServiceIdentityTreeNode(device);
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
                newNode.Parent = parentNode;
                parentNode.ForEach(node => node.Children.Add(newNode));
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
                child.Parent = Option.Some(newNode);
                newNode.Children.Add(child);
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
            public ServiceIdentity Identity { get; private set; }
            public Option<ServiceIdentityTreeNode> Parent;
            public List<ServiceIdentityTreeNode> Children;

            public ServiceIdentityTreeNode(ServiceIdentity identity)
            {
                this.Identity = Preconditions.CheckNotNull(identity);
                this.Parent = Option.None<ServiceIdentityTreeNode>();
                this.Children = new List<ServiceIdentityTreeNode>();
            }
        }
    }
}
