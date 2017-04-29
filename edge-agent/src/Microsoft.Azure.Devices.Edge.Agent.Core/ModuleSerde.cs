// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class ModuleSerde
    {
        static readonly JsonSerializerSettings SerializerSettings =
            new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

        public string Serialize(IModule module) => JsonConvert.SerializeObject(module, SerializerSettings);

        public T Deserialize<T>(string json)
            where T : IModule
        {
            // This try/catch is needed because NewtonSoft Deserialize is calling the constructor even
            // if the Name parameter is not present on the JSON.
            try
            {
                return JsonConvert.DeserializeObject<T>(json, SerializerSettings);
            }
            catch (ArgumentException e)
            {
                throw new JsonSerializationException(e.Message, e);
            }
        }
    }
}
