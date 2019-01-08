// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Serde
{
    using System;
    using Newtonsoft.Json;

    public class ModuleSerde : ISerde<IModule>
    {
        ModuleSerde()
        {
        }

        public static ModuleSerde Instance { get; } = new ModuleSerde();

        public string Serialize(IModule module) => JsonConvert.SerializeObject(module);

        public IModule Deserialize(string json) => this.Deserialize<IModule>(json);

        public T Deserialize<T>(string json)
            where T : IModule
        {
            // This try/catch is needed because NewtonSoft Deserialize is calling the constructor even
            // if the Name parameter is not present on the JSON.
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

        public IModule Deserialize(string json, Type serializerType)
        {
            // This try/catch is needed because NewtonSoft Deserialize is calling the constructor even
            // if the Name parameter is not present on the JSON.
            try
            {
                return (IModule)JsonConvert.DeserializeObject(json, serializerType);
            }
            catch (ArgumentNullException e)
            {
                throw new JsonSerializationException(e.Message);
            }
        }
    }
}
