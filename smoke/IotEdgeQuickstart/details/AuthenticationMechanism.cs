// Copyright (c) Microsoft. All rights reserved.

namespace IotEdgeQuickstart.Details
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class AuthenticationMechanism
    {
        public Option<string> ConnectionString { get; }

        public Option<DPSAttestation> DpsConfig { get; }

        public AuthenticationMechanism(string connectionString)
        {
            ConnectionString = Option.Some(connectionString);
            DpsConfig = Option.None<DPSAttestation>();
        }
    }
}
