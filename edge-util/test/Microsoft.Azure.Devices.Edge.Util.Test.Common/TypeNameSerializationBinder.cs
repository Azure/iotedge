// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// This class helps JSON.NET deserialize objects when the JSON has a special "$type"
    /// property to indicate what object to deserialize the object into. Taken from:
    ///     http://james.newtonking.com/archive/2011/11/19/json-net-4-0-release-4-bug-fixes
    /// </summary>
    public class TypeNameSerializationBinder : ISerializationBinder
    {
        public readonly string TypeFormat;

        // KnownTypes provides a allow-list of allowed types for deserialization, mitigating potential security vulnerabilities
        // associated with uncontrolled type deserialization when using TypeNameHandling
        // Secure Deserialization Best Practices: https://liquid.microsoft.com/Web/Object/Read/MS.Security/Requirements/Microsoft.Security.SystemsADM.10010#Zguide
        // CodeQL Scanning Tool Warning: https://liquid.microsoft.com/Web/Object/Read/ScanningToolWarnings/Requirements/CodeQL.SM02211#Zguide
        public IList<Type> KnownTypes { get; set; }

        public TypeNameSerializationBinder(string typeFormat)
        {
            this.TypeFormat = typeFormat;
        }

        public TypeNameSerializationBinder(string typeFormat, IList<Type> knownTypes)
        {
            this.TypeFormat = typeFormat;
            this.KnownTypes = knownTypes;
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = null;
            typeName = serializedType.Name;
        }

        public Type BindToType(string assemblyName, string typeName)
        {
            Type resolvedType = Type.GetType(string.Format(this.TypeFormat, typeName), true);
            return this.KnownTypes.SingleOrDefault(t => t == resolvedType );
        }
    }
}
