// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    // TODO add unit tests
    public class OverrideJsonIgnoreOfBaseClassContractResolver : CamelCasePropertyNamesContractResolver
    {
        public OverrideJsonIgnoreOfBaseClassContractResolver(Dictionary<Type, string[]> names)
        {
            this.names = new Dictionary<Type, HashSet<string>>();

            foreach (var (type, properties) in names)
            {
                if (!this.names.TryGetValue(type, out var existing))
                {
                    existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    this.names.Add(type, existing);
                }

                foreach (var property in properties)
                {
                    existing.Add(property);
                }
            }
        }

        readonly Dictionary<Type, HashSet<string>> names;

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (this.names.TryGetValue(property.DeclaringType, out var properties) && properties.Contains(property.PropertyName))
            {
                property.Ignored = false;
                property.ShouldSerialize = propInstance => true;
            }

            return property;
        }
    }
}
