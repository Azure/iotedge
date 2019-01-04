// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Json
{
    using System;
    using Newtonsoft.Json;

    public class OptionConverter<T> : JsonConverter
    {
        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Option<T> option)
            {
                serializer.Serialize(writer, option.OrDefault());
            }
            else
            {
                serializer.Serialize(writer, value);
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) => throw new NotSupportedException();

        public override bool CanConvert(Type type) => type.IsGenericType && typeof(Option<T>) == type.GetGenericTypeDefinition();
    }
}
