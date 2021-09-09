// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class AuthChainHelpers
    {
        public static string[] GetAuthChainIds(string authChain)
        {
            Preconditions.CheckNonWhiteSpace(authChain, nameof(authChain));
            return authChain.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static Option<string> GetAuthTarget(Option<string> authChain)
        {
            if (!authChain.HasValue)
            {
                return Option.None<string>();
            }

            string authChainString = Preconditions.CheckNonWhiteSpace(authChain.OrDefault(), nameof(authChain));
            string[] authChainIds = GetAuthChainIds(authChainString);

            // The auth target is always the first element of the auth-chain
            return authChainIds.FirstOption(id => true);
        }

        public static Option<string> GetAuthParent(Option<string> authChain)
        {
            return authChain.Match(
                chain =>
                {
                    string[] authChainIds = GetAuthChainIds(chain);
                    // The auth target is second element after the target
                    return authChainIds.Skip(1).FirstOption(id => true);
                },
                () => Option.None<string>());
        }

        public static Option<string> GetActorDeviceId(Option<string> authChain)
        {
            if (!authChain.HasValue)
            {
                return Option.None<string>();
            }

            string authChainString = Preconditions.CheckNonWhiteSpace(authChain.OrDefault(), nameof(authChain));
            string[] authChainIds = GetAuthChainIds(authChainString);

            // OnBehalfOf must have at least 1 leaf/module and 1 Edge in the chain
            if (authChainIds.Length <= 1)
            {
                return Option.None<string>();
            }

            // The actor Edge is always the last element in the chain
            return Option.Some(authChainIds.Last());
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
            targetDeviceId = string.Empty;

            if (string.IsNullOrWhiteSpace(authChain))
            {
                return false;
            }

            // The target device is the first Identity in the provided authchain, which is a device identity.
            var actorAuthChainIds = authChain.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            if (actorAuthChainIds.Length > 0)
            {
                var ids = actorAuthChainIds[0].Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                if (ids.Length == 1)
                {
                    targetDeviceId = ids[0];
                    return true;
                }
            }

            return false;
        }
    }
}
