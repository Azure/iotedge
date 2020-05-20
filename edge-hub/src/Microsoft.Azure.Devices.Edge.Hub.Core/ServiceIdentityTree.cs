// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is a tree implementation that uses the ParentScopes property
    /// to reconstruct the hierarchy of modules/devices under the current
    /// Edge device in a nested Edge environment.
    /// </summary>
    class ServiceIdentityTree
    {
        public event EventHandler<string> ServiceIdentityRemoved;

        readonly IDictionary<string, ServiceIdentityTreeNode> nodes;
        readonly string rootDeviceId;

        public ServiceIdentityTree(string rootDeviceId)
        {
            this.rootDeviceId = Preconditions.CheckNonWhiteSpace(rootDeviceId, nameof(rootDeviceId));
            this.nodes = new Dictionary<string, ServiceIdentityTreeNode>();
        }

        public void InsertOrUpdate(ServiceIdentity identity)
        {
            // There should always be a valid ServiceIdentity
            Preconditions.CheckNotNull(identity, nameof(identity));

            if (this.Contains(identity.Id))
            {
                // Update case - this is just remove + re-insert,
                // except we don't remove any existing children.
                // There's no scenario where losing a link to
                // either the parent or a child wouldn't result
                // in a call to RemoveRecursive().
                this.RemoveSingleNode_NoCallback(identity.Id);
            }

            // Insert case
            if (identity.IsModule)
            {
                this.InsertModuleIdentity(identity);
            }
            else
            {
                this.InsertDeviceIdentity(identity);
            }
        }

        public void Remove(string id)
        {
            // In the case of an explicit removal, the device/module is
            // no longer part of the nested hierarchy, so we should
            // remove any children of the device as well.
            this.RemoveRecursive(id);
        }

        public bool Contains(string id)
        {
            return this.nodes.ContainsKey(id);
        }

        public Option<ServiceIdentity> Get(string id)
        {
            return this.nodes.TryGetValue(id, out ServiceIdentityTreeNode treeNode)
                    ? Option.Some(treeNode.Identity)
                    : Option.None<ServiceIdentity>();
        }

        public IList<string> GetAllIds()
        {
            return this.nodes.Select(kvp => kvp.Value.Identity.Id).ToList();
        }

        public bool TryGetAuthChain(string id, out string authChain)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            authChain = string.Empty;

            // Find the target device
            if (this.nodes.TryGetValue(id, out ServiceIdentityTreeNode treeNode))
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

            return false;
        }

        void InsertModuleIdentity(ServiceIdentity module)
        {
            var newNode = new ServiceIdentityTreeNode(module);
            this.nodes.Add(module.Id, newNode);

            if (this.nodes.TryGetValue(module.DeviceId, out ServiceIdentityTreeNode parentDeviceNode))
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

        void InsertDeviceIdentity(ServiceIdentity device)
        {
            var newNode = new ServiceIdentityTreeNode(device);
            this.nodes.Add(device.Id, newNode);

            // Check if there's an existing parent for this device
            device.ParentScopes.ForEach(parentScopeId =>
            {
                // Look for the parent based on scope ID, it's okay if we can't find a parent
                // node. It's possible that the identity for the parent hasn't came in yet.
                // In this case we'll just leave the new identity as a dangling node, and
                // the parent device identity should come in at a later point.
                Option<ServiceIdentityTreeNode> parentNode = this.FindDeviceByScopeId(parentScopeId);

                // Hook up the new node into the tree
                newNode.Parent = parentNode;
                parentNode.ForEach(node => node.Children.Add(newNode));
            });

            // Check if there are any dangling children that can now be hooked up,
            // this include placing the new node as the new root.
            List<ServiceIdentityTreeNode> danglingChildren =
                this.nodes
                .Select(kvp => kvp.Value)
                .Where(s => s.Identity.ParentScopes.HasValue && s.Identity.ParentScopes.Equals(device.DeviceScope))
                .ToList();

            foreach (ServiceIdentityTreeNode child in danglingChildren)
            {
                child.Parent = Option.Some(newNode);
                newNode.Children.Add(child);
            }
        }

        void RemoveSingleNode_NoCallback(string id)
        {
            if (this.nodes.TryGetValue(id, out ServiceIdentityTreeNode target))
            {
                // Unhook from the parent
                target.Parent.ForEach(parent => parent.Children.Remove(target));

                // Unhook the children
                target.Children.ForEach(child => child.Parent = Option.None<ServiceIdentityTreeNode>());

                // Remove
                this.nodes.Remove(id);
            }
        }

        void RemoveRecursive(string id)
        {
            if (this.nodes.TryGetValue(id, out ServiceIdentityTreeNode target))
            {
                // Unhook from the parent
                target.Parent.ForEach(parent => parent.Children.Remove(target));

                // Recursively remove each child
                var childrenSnapshot = new List<ServiceIdentityTreeNode>(target.Children);
                childrenSnapshot.ForEach(child => this.RemoveRecursive(child.Identity.Id));

                // Remove self after recursion
                this.nodes.Remove(id);

                // Notify callbacks
                this.ServiceIdentityRemoved?.Invoke(this, id);
            }
        }

        Option<ServiceIdentityTreeNode> FindDeviceByScopeId(string scopeId)
        {
            // Look for Edge devices with a matching scope ID
            List<ServiceIdentityTreeNode> nodes =
                this.nodes
                .Select(kvp => kvp.Value)
                .Where(serviceIdentity => serviceIdentity.Identity.IsEdgeDevice && serviceIdentity.Identity.DeviceScope.Contains(scopeId))
                .ToList();

            if (nodes.Count() > 0)
            {
                // There shouldn't be more than one node, but even if there is
                // for some reason, we can just use the first one
                return Option.Some(nodes.First());
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
