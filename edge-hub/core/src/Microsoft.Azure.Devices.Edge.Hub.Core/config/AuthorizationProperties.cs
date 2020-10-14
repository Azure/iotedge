// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary>
    /// DTO that is used to deserialize MQTT Broker Authz Policy from EdgeHub twin
    /// into <see cref="AuthorizationConfig" />.
    /// </summary>
    public class AuthorizationProperties : List<AuthorizationProperties.Statement>
    {
        public class Statement
        {
            [JsonConstructor]
            public Statement(IList<string> identities, IList<Rule> allow, IList<Rule> deny)
            {
                this.Identities = identities;
                this.Allow = allow;
                this.Deny = deny;
            }

            [JsonProperty(PropertyName = "identities")]
            public IList<string> Identities { get; }

            [JsonProperty(PropertyName = "allow")]
            public IList<Rule> Allow { get; }

            [JsonProperty(PropertyName = "deny")]
            public IList<Rule> Deny { get; }
        }

        public class Rule
        {
            [JsonConstructor]
            public Rule(IList<string> operations, IList<string> resources)
            {
                this.Operations = operations;
                this.Resources = resources;
            }

            [JsonProperty(PropertyName = "operations")]
            public IList<string> Operations { get; }

            [JsonProperty(PropertyName = "resources")]
            public IList<string> Resources { get; }
        }
    }
}
