// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class AuthChainHelpers
    {
        public static string[] GetAuthChainIds(string authChain)
        {
            Preconditions.CheckNonWhiteSpace(authChain, nameof(authChain));
            return authChain.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static bool ValidateAuthChain(string actorDeviceId, string targetId, string authChain)
        {
            Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId));
            Preconditions.CheckNonWhiteSpace(targetId, nameof(targetId));
            Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId));
            string[] authChainIds = GetAuthChainIds(authChain);

            // Should have at least 1 element in the chain
            if (authChainIds.Length < 1)
            {
                return false;
            }

            // The first element of the authChain should be the target identity
            if (authChainIds[0] != targetId)
            {
                return false;
            }

            // The actor device should be in the authChain
            bool targetAuthChainHasActor = false;
            foreach (string id in authChainIds)
            {
                if (id == actorDeviceId)
                {
                    targetAuthChainHasActor = true;
                    break;
                }
            }

            if (!targetAuthChainHasActor)
            {
                return false;
            }

            return true;
        }

        public static bool TryGetTargetDeviceId(string authChain, out string targetDeviceId)
        {
            string targetId = string.Empty;
            targetDeviceId = string.Empty;

            if (TryGetTargetId(authChain, out targetId))
            {
                var ids = targetId.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                targetDeviceId = ids[0];
                return true;
            }

            return false;
        }

        public static bool TryGetTargetId(string authChain, out string targetId)
        {
            targetId = string.Empty;

            // The target device is the first ID in the provided authchain,
            // which could be a module ID of the format "deviceId/moduleId".
            var actorAuthChainIds = authChain.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            if (actorAuthChainIds.Length > 0)
            {
                var ids = actorAuthChainIds[0].Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                if (ids.Length == 1 || ids.Length == 2)
                {
                    targetId = actorAuthChainIds[0];
                    return true;
                }
            }

            return false;
        }
    }
}
