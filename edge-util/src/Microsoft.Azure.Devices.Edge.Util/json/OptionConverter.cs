// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Json
{
    using System;
    using Newtonsoft.Json;

    public class OptionConverter<T> : JsonConverter
    {
        readonly bool nullOnNone;

        public OptionConverter()
            : this(false)
        {
        }

        public OptionConverter(bool nullOnNone)
        {
            this.nullOnNone = nullOnNone;
        }

        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Option<T> option)
            {
                if (option.HasValue || !this.nullOnNone)
                {
                    serializer.Serialize(writer, option.OrDefault());
                }
                else
                {
                    serializer.Serialize(writer, null);
                }
            }
            else
            {
                serializer.Serialize(writer, value);
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            => throw new NotSupportedException();

        public override bool CanConvert(Type type) => type.IsGenericType && typeof(Option<T>) == type.GetGenericTypeDefinition();
    }
}
