// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class AuthChainHelpers
    {
        public static string[] GetAuthChainIds(string authChain) => authChain.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        public static bool ValidateAuthChain(string actorDeviceId, string targetId, string authChain)
        {
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
