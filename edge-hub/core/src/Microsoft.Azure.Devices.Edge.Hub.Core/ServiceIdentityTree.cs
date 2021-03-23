// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// This is a tree implementation that uses the ParentScopes property
    /// to reconstruct the hierarchy of modules/devices under the current
    /// Edge device in a nested Edge environment.
    /// </summary>
    public class ServiceIdentityTree : IServiceIdentityHierarchy
    {
        readonly string actorDeviceId;
        AsyncLock nodesLock = new AsyncLock();
        Dictionary<string, ServiceIdentityTreeNode> nodes;

        public ServiceIdentityTree(string actorDeviceId)
        {
            this.actorDeviceId = Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId));
            this.nodes = new Dictionary<string, ServiceIdentityTreeNode>();
        }

        public string GetActorDeviceId() => this.actorDeviceId;

        public async Task InsertOrUpdate(ServiceIdentity identity)
        {
            // There should always be a valid ServiceIdentity
            Preconditions.CheckNotNull(identity, nameof(identity));

            using (await this.nodesLock.LockAsync())
            {
                bool isUpdate = false;

                if (this.nodes.ContainsKey(identity.Id))
                {
                    // Update case - this is just remove + re-insert
                    isUpdate = true;
                    this.RemoveSingleNode(identity.Id);
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

                if (isUpdate)
                {
                    Events.NodeUpdated(identity.Id);
                }
                else
                {
                    Events.NodeAdded(identity.Id);
                }
            }
        }

        public async Task Remove(string id)
        {
            using (await this.nodesLock.LockAsync())
            {
                this.RemoveSingleNode(id);
            }

            Events.NodeRemoved(id);
        }

        public async Task<bool> Contains(string id)
        {
            using (await this.nodesLock.LockAsync())
            {
                return this.nodes.ContainsKey(id);
            }
        }

        public async Task<Option<ServiceIdentity>> Get(string id)
        {
            using (await this.nodesLock.LockAsync())
            {
                return this.nodes.TryGetValue(id, out ServiceIdentityTreeNode treeNode)
                    ? Option.Some(treeNode.Identity)
                    : Option.None<ServiceIdentity>();
            }
        }

        public async Task<IList<string>> GetAllIds()
        {
            using (await this.nodesLock.LockAsync())
            {
                return this.nodes.Select(kvp => kvp.Value.Identity.Id).ToList();
            }
        }

        public async Task<Option<string>> GetAuthChain(string targetId)
        {
            Preconditions.CheckNonWhiteSpace(targetId, nameof(targetId));

            // Auth-chain for the acting device (when there aren't any nodes in the tree yet)
            if (targetId == this.actorDeviceId)
            {
                return Option.Some(this.actorDeviceId);
            }
            else if (targetId == this.actorDeviceId + "/" + Constants.EdgeHubModuleId)
            {
                return Option.Some(this.actorDeviceId + "/" + Constants.EdgeHubModuleId + ";" + this.actorDeviceId);
            }

            using (await this.nodesLock.LockAsync())
            {
                // Auth-chain for a child somewhere in the tree
                if (this.nodes.TryGetValue(targetId, out ServiceIdentityTreeNode treeNode))
                {
                    if (treeNode.AuthChain.HasValue)
                    {
                        // Check every Edge device in the authchain for disabled devices
                        string[] authChainIds = treeNode.AuthChain.Map(chain => chain.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)).OrDefault();

                        foreach (string chainId in authChainIds)
                        {
                            if (!this.nodes.TryGetValue(chainId, out ServiceIdentityTreeNode node))
                            {
                                Events.AuthChainMissingDevice(targetId, chainId);
                                return Option.None<string>();
                            }

                            ServiceIdentity identity = node.Identity;
                            if (identity.IsEdgeDevice && identity.Status == ServiceIdentityStatus.Disabled)
                            {
                                // Chain is unuseable if one of the devices is disabled
                                Events.AuthChainDisabled(targetId, chainId);
                                return Option.None<string>();
                            }
                        }
                    }

                    return treeNode.AuthChain;
                }
            }

            return Option.None<string>();
        }

        public async Task<Try<string>> TryGetAuthChain(string targetId)
        {
            Preconditions.CheckNonWhiteSpace(targetId, nameof(targetId));

            // Auth-chain for the acting device (when there aren't any nodes in the tree yet)
            if (targetId == this.actorDeviceId)
            {
                return Try.Success(this.actorDeviceId);
            }
            else if (targetId == this.actorDeviceId + "/" + Constants.EdgeHubModuleId)
            {
                return Try.Success(this.actorDeviceId + "/" + Constants.EdgeHubModuleId + ";" + this.actorDeviceId);
            }

            using (await this.nodesLock.LockAsync())
            {
                // Auth-chain for a child somewhere in the tree
                if (this.nodes.TryGetValue(targetId, out ServiceIdentityTreeNode treeNode))
                {
                    return treeNode.AuthChain.Match(
                        chain => Try.Success(chain),
                        () => Try<string>.Failure(new DeviceInvalidStateException("Device is out of scope.")));
                }
                else
                {
                    return Try<string>.Failure(new DeviceInvalidStateException("Device is out of scope."));
                }
            }
        }

        public async Task<Option<string>> GetEdgeAuthChain(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));

            // By default, the Edge device is just the target itself
            string edgeDeviceId = id;

            // Get the auth-chain of the closest Edge device to the target
            using (await this.nodesLock.LockAsync())
            {
                if (this.nodes.TryGetValue(id, out ServiceIdentityTreeNode treeNode))
                {
                    if (!treeNode.Identity.IsEdgeDevice)
                    {
                        // For modules and leaf devices, we need the auth-chain
                        // of the parent Edge device
                        ServiceIdentityTreeNode parentEdge = treeNode.Parent.Expect(() => new InvalidOperationException($"Cannot get parent Edge auth-chain for dangling identity {id}"));
                        edgeDeviceId = parentEdge.Identity.Id;
                    }
                }
            }

            return await this.GetAuthChain(edgeDeviceId);
        }

        public async Task<IList<ServiceIdentity>> GetImmediateChildren(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));

            var children = new List<ServiceIdentity>();

            using (await this.nodesLock.LockAsync())
            {
                if (this.nodes.TryGetValue(id, out ServiceIdentityTreeNode treeNode))
                {
                    IList<ServiceIdentityTreeNode> childNodes = treeNode.GetAllChildren();
                    children.AddRange(treeNode.GetAllChildren().Select(child => child.Identity));
                }
            }

            return children;
        }

        void InsertModuleIdentity(ServiceIdentity module)
        {
            var newNode = new ServiceIdentityTreeNode(module, Option.None<string>());
            this.nodes.Add(module.Id, newNode);

            if (this.nodes.TryGetValue(module.DeviceId, out ServiceIdentityTreeNode parentDeviceNode))
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

        void InsertDeviceIdentity(ServiceIdentity device)
        {
            // Root device is the base-case for constructing the authchain,
            // child devices derive their authchain from the parent
            var newNode = device.Id == this.actorDeviceId ?
                new ServiceIdentityTreeNode(device, Option.Some(this.actorDeviceId)) :
                new ServiceIdentityTreeNode(device, Option.None<string>());

            this.nodes.Add(device.Id, newNode);

            // Check if there's an existing parent for this device
            foreach (string parentScopeId in device.ParentScopes)
            {
                // Look for the parent based on scope ID, it's okay if we can't find a parent
                // node. It's possible that the identity for the parent hasn't came in yet.
                // In this case we'll just leave the new identity as a dangling node, and
                // the parent device identity should come in at a later point.
                Option<ServiceIdentityTreeNode> parentNode = this.FindDeviceByScopeId(parentScopeId);

                // Hook up the new node into the tree
                parentNode.ForEach(p =>
                {
                    p.AddChild(newNode);
                });
            }

            if (device.IsEdgeDevice)
            {
                // Check if there are any dangling child devices that can now be hooked up,
                // this include placing the new node as the new root.
                List<ServiceIdentityTreeNode> danglingChildren =
                    this.nodes
                    .Select(kvp => kvp.Value)
                    .Where(s => s.Identity.ParentScopes.Count() > 0 && s.Identity.ParentScopes.Contains(device.DeviceScope.OrDefault()))
                    .ToList();

                // Also check for any modules that should be parented to this new device
                danglingChildren.AddRange(this.nodes
                    .Select(kvp => kvp.Value)
                    .Where(s => s.Identity.IsModule && s.Identity.DeviceId == device.DeviceId)
                    .ToList()
                    .AsEnumerable());

                foreach (ServiceIdentityTreeNode child in danglingChildren)
                {
                    newNode.AddChild(child);
                }
            }
        }

        void RemoveSingleNode(string id)
        {
            if (this.nodes.TryGetValue(id, out ServiceIdentityTreeNode target))
            {
                // Unhook from parent
                target.Parent.ForEach(p => p.RemoveChild(target));

                // Unhook the children
                target.RemoveAllChildren();

                // Remove the node itself
                this.nodes.Remove(id);
            }
        }

        Option<ServiceIdentityTreeNode> FindDeviceByScopeId(string scopeId)
        {
            // Look for Edge devices with a matching scope ID
            List<ServiceIdentityTreeNode> devices =
                this.nodes
                .Select(kvp => kvp.Value)
                .Where(serviceIdentity => serviceIdentity.Identity.IsEdgeDevice && serviceIdentity.Identity.DeviceScope.Contains(scopeId))
                .ToList();

            if (devices.Count() > 0)
            {
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
            // Only allow up to 5 Edge devices to be linked together
            static readonly int MaxNestingDepth = 5;

            public ServiceIdentity Identity { get; }
            public Option<string> AuthChain { get; private set; }
            public Option<ServiceIdentityTreeNode> Parent { get; private set; }

            List<ServiceIdentityTreeNode> children;
            int currentDepth;

            public ServiceIdentityTreeNode(ServiceIdentity identity, Option<string> authChain)
            {
                this.Identity = Preconditions.CheckNotNull(identity);
                this.Parent = Option.None<ServiceIdentityTreeNode>();
                this.children = new List<ServiceIdentityTreeNode>();
                this.AuthChain = authChain;
                this.currentDepth = 0;
            }

            public void AddChild(ServiceIdentityTreeNode childNode)
            {
                if (!this.Identity.IsEdgeDevice)
                {
                    throw new ArgumentException($"{this.Identity.Id} is not an Edge device, only Edge devices can have children");
                }

                this.children.Add(childNode);
                childNode.Parent = Option.Some(this);
                childNode.UpdateAuthChainFromParent(this, this.currentDepth + 1);
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

            public IList<ServiceIdentityTreeNode> GetAllChildren() => this.children;

            void UpdateAuthChainFromParent(ServiceIdentityTreeNode parentNode, int traveled)
            {
                // The max allowed depth for leaf device and modules is 5 + 1,
                // because they don't count towards the nesting level.
                if (traveled >= MaxNestingDepth + 1)
                {
                    // Something went wrong, leaf devices and modules should never exceed the maximum.
                    throw new InvalidOperationException($"Nesting depth exceeded maximum at {this.Identity.Id}, check for potential cyclic dependencies");
                }

                if (this.Identity.IsEdgeDevice)
                {
                    this.currentDepth = parentNode.currentDepth + 1;
                }

                if (this.Identity.IsEdgeDevice && traveled >= MaxNestingDepth)
                {
                    // We ended up with more than the max allowed depth, this can happen if two separate
                    // chains got stitched together. In this case, we discard everything past the maximum
                    // by removing their auth chains. If the customer intentionally configured more layers
                    // than the max, we'll continue to work with just the first 5 nested layers.
                    Events.MaxDepthExceeded(this.Identity.Id);
                    this.AuthChain = Option.None<string>();
                    this.children.ForEach(child => child.RemoveAuthChain());
                }
                else
                {
                    // Our auth chain and depth is inherited from the parent's chain
                    this.AuthChain = parentNode.AuthChain.Map(parentChain => this.MakeAuthChainFromParent(parentChain));
                    this.AuthChain.ForEach(authchain => Events.AuthChainAdded(this.Identity.Id, authchain, this.currentDepth));

                    // Recursively update all children to re-calculate their authchains and depth
                    this.children.ForEach(child => child.UpdateAuthChainFromParent(this, traveled + 1));
                }
            }

            void RemoveAuthChain() => this.RemoveAuthChainRecursive(0);

            void RemoveAuthChainRecursive(int traveled)
            {
                if (traveled >= 2 * MaxNestingDepth)
                {
                    // The max length of a nested chain we can temporarily achieve
                    // occurs when we try to stitch together two full hierarchies.
                    // Anything past that is not valid and we can stop recursing.
                    return;
                }

                this.AuthChain = Option.None<string>();
                Events.AuthChainRemoved(this.Identity.Id);

                // Recursively remove the authchains of children
                this.children.ForEach(child => child.RemoveAuthChainRecursive(traveled + 1));
            }

            string MakeAuthChainFromParent(string parentAuthChain)
            {
                return this.Identity.Id + ";" + parentAuthChain;
            }
        }
    }

    static class Events
    {
        const int IdStart = HubCoreEventIds.ServiceIdentityTree;
        static readonly ILogger Log = Logger.Factory.CreateLogger<IDeviceScopeIdentitiesCache>();

        enum EventIds
        {
            NodeAdded = IdStart,
            NodeRemoved,
            NodeUpdated,
            AuthChainAdded,
            AuthChainRemoved,
            MaxDepthExceeded,
            AuthChainMissingDevice,
            AuthChainDisabled,
        }

        public static void NodeAdded(string id) =>
            Log.LogInformation((int)EventIds.NodeAdded, $"Add node: {id}");

        public static void NodeRemoved(string id) =>
            Log.LogInformation((int)EventIds.NodeRemoved, $"Removed node: {id}");

        public static void NodeUpdated(string id) =>
            Log.LogInformation((int)EventIds.NodeUpdated, $"Updated node: {id}");

        public static void AuthChainAdded(string id, string authChain, int depth) =>
            Log.LogDebug((int)EventIds.AuthChainAdded, $"Auth-chain added for: {id}, at depth: {depth}, {authChain}");

        public static void AuthChainRemoved(string id) =>
            Log.LogDebug((int)EventIds.AuthChainRemoved, $"Auth-chain removed for: {id}");

        public static void MaxDepthExceeded(string edgeDeviceId) =>
            Log.LogWarning((int)EventIds.MaxDepthExceeded, $"Nested hierarchy contains more than maximum allowed layers, discarding {edgeDeviceId}");

        public static void AuthChainMissingDevice(string targetId, string missingDeviceId) =>
            Log.LogWarning((int)EventIds.AuthChainMissingDevice, $"Cannot use auth-chain for {targetId}, parent device {missingDeviceId} is missing");

        public static void AuthChainDisabled(string targetId, string disabledId) =>
            Log.LogWarning((int)EventIds.AuthChainDisabled, $"Cannot use auth-chain for {targetId}, parent device {disabledId} is disabled");
    }
}
