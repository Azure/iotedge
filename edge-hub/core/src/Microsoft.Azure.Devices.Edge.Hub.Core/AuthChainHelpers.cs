// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class AuthChainHelpers
    {
        public static bool ValidateAuthChain(string actorDeviceId, string targetId, string authChain)
        {
            var authChainIds = authChain.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

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
