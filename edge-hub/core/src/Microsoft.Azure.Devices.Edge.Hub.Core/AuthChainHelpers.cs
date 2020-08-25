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
    }
}
