// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class ModuleSerde
    {
        public string Serialize(IModule module) => JsonConvert.SerializeObject(
            module,
            new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

        public T Deserialize<T>(string json)
            where T : IModule
        {
            //This try/catch is needed because NewtonSoft Deserialize is calling the constructor even 
            //if the Name parameter is not presente on the JSON.
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (ArgumentNullException e)
            {
                throw new JsonSerializationException(e.Message);
            }
        } 
    }
}
