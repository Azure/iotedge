// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.E2E.Test
{
    using System;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// This class helps JSON.NET deserialize objects when the JSON has a special "$type"
    /// property to indicate what object to deserialize the object into. Taken from:
    ///     http://james.newtonking.com/archive/2011/11/19/json-net-4-0-release-4-bug-fixes
    /// </summary>
    class TypeNameSerializationBinder : ISerializationBinder
    {
        public readonly string TypeFormat;

        public TypeNameSerializationBinder(string typeFormat)
        {
            this.TypeFormat = typeFormat;
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = null;
            typeName = serializedType.Name;
        }

        public Type BindToType(string assemblyName, string typeName)
        {
            return Type.GetType(string.Format(this.TypeFormat, typeName), true);
        }
    }
}
