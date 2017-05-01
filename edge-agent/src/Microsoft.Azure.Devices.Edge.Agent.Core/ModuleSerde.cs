// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class ModuleSerde
    {
        public static ModuleSerde Instance { get; } = new ModuleSerde();

        ModuleSerde()
        {
        }

        readonly JsonSerializerSettings jsonSerializerSetting = new JsonSerializerSettings{ContractResolver = new CamelCasePropertyNamesContractResolver()};

        public string Serialize(IModule module) => JsonConvert.SerializeObject(module, this.jsonSerializerSetting);

        public T Deserialize<T>(string json)
            where T : IModule
        {
            //This try/catch is needed because NewtonSoft Deserialize is calling the constructor even 
            //if the Name parameter is not present on the JSON.
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (ArgumentNullException e)
            {
                throw new JsonSerializationException(e.Message);
            }
            catch (ArgumentException e)
            {
                throw new JsonSerializationException(e.Message);
            }
        }
        
        public IModule Deserialize(string json, System.Type serializerType)
        {
            //This try/catch is needed because NewtonSoft Deserialize is calling the constructor even 
            //if the Name parameter is not present on the JSON.
            try
            {
                object returnObject = JsonConvert.DeserializeObject(json, serializerType);

                return (IModule)returnObject;
            }
            catch (ArgumentNullException e)
            {
                throw new JsonSerializationException(e.Message);
            }
        }
    }
}
