// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public static class EdgeDeploymentSerialization
    {
        public static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new OverrideJsonIgnoreOfBaseClassContractResolver(
                new Dictionary<Type, string[]>
                {
                    [typeof(KubernetesModule)] = new[] { nameof(KubernetesModule.Name) }
                })
            {
                // Environment variable (env) property JSON casing should be left alone
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    ProcessDictionaryKeys = false
                }
            }
        };
    }
}
