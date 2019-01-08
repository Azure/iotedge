// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Serde
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class DiffSerde : ISerde<Diff>
    {
        readonly IDictionary<string, Type> converters;

        public DiffSerde(IDictionary<string, Type> deserializerTypes)
        {
            this.converters = new Dictionary<string, Type>(Preconditions.CheckNotNull(deserializerTypes, nameof(deserializerTypes)), StringComparer.OrdinalIgnoreCase);
        }

        public string Serialize(Diff diff) => throw new NotSupportedException();

        public T Deserialize<T>(string json)
            where T : Diff => throw new NotSupportedException();

        public Diff Deserialize(string json)
        {
            var diffConverter = new DiffJsonConverter(this.converters);

            return JsonConvert.DeserializeObject<Diff>(json, diffConverter);
        }
    }

    class DiffJsonConverter : JsonConverter
    {
        readonly IDictionary<string, Type> converters;

        public DiffJsonConverter(IDictionary<string, Type> deserializerTypes)
        {
            this.converters = new Dictionary<string, Type>(deserializerTypes, StringComparer.OrdinalIgnoreCase);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);

            var updateList = new List<IModule>();
            var removeList = new List<string>();

            var modules = obj.Get<JToken>("modules");

            foreach (JToken xtoken in modules.Children())
            {
                JToken xtokenFirst = xtoken.First;
                string name = xtokenFirst.Path.Split('.')[1];

                if (xtokenFirst.HasValues)
                {
                    JObject obj2 = JObject.Parse(xtokenFirst.ToString());

                    var converterType = obj2.Get<JToken>("type");

                    if (!this.converters.TryGetValue(converterType.Value<string>(), out Type serializeType))
                    {
                        throw new JsonSerializationException($"Could not find right converter given type {converterType.Value<string>()}");
                    }

                    IModule module = ModuleSerde.Instance.Deserialize(xtokenFirst.ToString(), serializeType);
                    module.Name = name;
                    updateList.Add(module);
                }
                else
                {
                    removeList.Add(name);
                }
            }

            return new Diff(updateList, removeList);
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(Diff);
    }
}
