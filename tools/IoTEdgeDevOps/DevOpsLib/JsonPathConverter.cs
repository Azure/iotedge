// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Linq;
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// This class is used to allow mapping property based on JsonPath defined in JsonProperty attribute for Json deserialization.
    /// Reference from https://stackoverflow.com/questions/33088462/can-i-specify-a-path-in-an-attribute-to-map-a-property-in-my-class-to-a-child-pr
    /// </summary>
    class JsonPathConverter : JsonConverter
    {
        public override bool CanWrite => false;

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            object targetObj = Activator.CreateInstance(objectType);

            foreach (PropertyInfo prop in objectType.GetProperties()
                .Where(p => p.CanRead && p.CanWrite))
            {
                JsonPropertyAttribute att = prop.GetCustomAttributes(true)
                    .OfType<JsonPropertyAttribute>()
                    .FirstOrDefault();

                string jsonPath = (att != null ? att.PropertyName : prop.Name);

                if (serializer.ContractResolver is DefaultContractResolver)
                {
                    var resolver = (DefaultContractResolver)serializer.ContractResolver;
                    jsonPath = resolver.GetResolvedPropertyName(jsonPath);
                }

                JToken token = jo.SelectToken(jsonPath);

                if (token != null && token.Type != JTokenType.Null)
                {
                    object value = token.ToObject(prop.PropertyType, serializer);
                    prop.SetValue(targetObj, value, null);
                }
            }

            return targetObj;
        }

        public override bool CanConvert(Type objectType)
        {
            // CanConvert is not called when [JsonConverter] attribute is used
            return false;
        }

        public override void WriteJson(
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
