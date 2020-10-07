// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Domain object that represents Authorization configuration for Edge Hub Module (MQTT Broker).
    ///
    /// This object is being eventually constructed from the EdgeHub twin's desired properties.
    /// See <see cref="EdgeHubDesiredProperties"/> for DTO.
    /// </summary>
    public class AuthorizationConfiguration
    {
        public bool Equals(StoreAndForwardConfiguration other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return false;
            // return this.TimeToLiveSecs == other.TimeToLiveSecs &&
            //     this.StoreLimits.Equals(other.StoreLimits);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as StoreAndForwardConfiguration);

        public override int GetHashCode()
        {
            unchecked
            {
                // int hashCode = (this.TimeToLiveSecs * 397) ^ this.TimeToLive.GetHashCode();
                // hashCode = (hashCode * 397) ^ this.StoreLimits.GetHashCode();
                return 397;
            }
        }
    }
}
