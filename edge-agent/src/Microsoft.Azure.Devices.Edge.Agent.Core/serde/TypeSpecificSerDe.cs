// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Serde
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// SerDe for objects with types that depend on the "type" property 
    /// (for example Docker specific types, etc.) 
    /// </summary>
    public class TypeSpecificSerDe<T> : ISerde<T>
    {
        readonly JsonSerializerSettings jsonSerializerSettings;

        /// <summary>
        /// DeserializerTypesMap is a map of input type to object to be deserialized. Something like this -
        /// IModule ->
        ///             "docker" -> DockerModule
        /// IRuntimeInfo ->
        ///             "docker" -> DockerRuntimeInfo
        /// This enables supporting multiple interfaces in an object, for different "types" like Docker.
        /// </summary>        
        public TypeSpecificSerDe(IDictionary<Type, IDictionary<string, Type>> deserializerTypesMap)
        {
            Preconditions.CheckNotNull(deserializerTypesMap, nameof(deserializerTypesMap));

            this.jsonSerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = new List<JsonConverter>
                {
                    new TypeSpecificJsonConverter(deserializerTypesMap)
                },
            };
        }        

        public string Serialize(T value)
        {
            return JsonConvert.SerializeObject(value, this.jsonSerializerSettings);
        }

        public T Deserialize(string json) => this.Deserialize<T>(json);

        public TU Deserialize<TU>(string json) where TU : T
        {
            try
            {
                return JsonConvert.DeserializeObject<TU>(json, this.jsonSerializerSettings);
            }
            catch (ArgumentNullException e)
            {
                throw new JsonSerializationException(e.Message);
            }
        }

        class TypeSpecificJsonConverter : JsonConverter
        {
            readonly IDictionary<Type, IDictionary<string, Type>> deserializerTypesMap;

            public TypeSpecificJsonConverter(IDictionary<Type, IDictionary<string, Type>> deserializerTypesMap)
            {
                this.deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>();
                foreach (KeyValuePair<Type, IDictionary<string, Type>> deserializerTypes in deserializerTypesMap)
                {
                    this.deserializerTypesMap[deserializerTypes.Key] = new Dictionary<string, Type>(deserializerTypes.Value, StringComparer.OrdinalIgnoreCase);
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                // The null check is required to gracefully handle a null object
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }

                if (!this.deserializerTypesMap.TryGetValue(objectType, out IDictionary<string, Type> deserializerTypeMap))
                {
                    throw new JsonSerializationException($"Could not find type {objectType.Name} in deserializerTypeMap");
                }

                JObject obj = JObject.Load(reader);
                var converterType = obj.Get<JToken>("type");

                if (!deserializerTypeMap.TryGetValue(converterType.Value<string>(), out Type serializeType))
                {
                    throw new JsonSerializationException($"Could not find right converter given a type {converterType.Value<string>()}");
                }

                object deserializedObject = JsonConvert.DeserializeObject(obj.ToString(), serializeType);
                return deserializedObject;
            }

            public override bool CanConvert(Type objectType) =>
                this.deserializerTypesMap.ContainsKey(objectType);
        }
    }
}
