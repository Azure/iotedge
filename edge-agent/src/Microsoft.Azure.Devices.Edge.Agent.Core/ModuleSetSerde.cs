// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;


    public class ModuleSetSerde
    {
        readonly IDictionary<string, Type> converters;

        readonly JsonSerializerSettings jsonSerializerSettings= new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver()};

    public ModuleSetSerde(IDictionary<string,Type> deserializerTypes)
        {
            this.converters = new Dictionary<string, Type>(Preconditions.CheckNotNull(deserializerTypes, nameof(deserializerTypes)), StringComparer.OrdinalIgnoreCase);
        }

        public string Serialize(ModuleSet moduleSet) => JsonConvert.SerializeObject(
            moduleSet,
            this.jsonSerializerSettings);

        public ModuleSet Deserialize(string json)
        {
            try
            {
                var moduleConverter = new ModuleJsonConverter(this.converters);

                return JsonConvert.DeserializeObject<ModuleSet>(json, moduleConverter);
            }
            catch (ArgumentNullException e)
            {
                throw new JsonSerializationException(e.Message);
            }
        }

        class ModuleJsonConverter : JsonConverter
        {
            readonly IDictionary<string, Type> converters;

            readonly ModuleSerde moduleSerde = ModuleSerde.Instance;

            public ModuleJsonConverter(IDictionary<string, System.Type> deserializerTypes)
            {
                this.converters = new Dictionary<string, Type>(deserializerTypes, StringComparer.OrdinalIgnoreCase);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JObject obj = JObject.Load(reader);

                
                if (!obj.TryGetValue("type", StringComparison.OrdinalIgnoreCase, out JToken converterType))
                {
                    throw new JsonSerializationException("Could not find right converter type.");
                }

                if (!this.converters.TryGetValue(converterType.Value<string>(), out Type serializeType))
                {
                    throw new JsonSerializationException($"Could not find right converter given a type {converterType.Value<string>()}");
                }

                return this.moduleSerde.Deserialize(obj.ToString(), serializeType);
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(IModule);
        }
    }
}