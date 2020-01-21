// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class DirectMethodMetadataJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DirectMethodReportMetadata);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            TestReportType testReportType = Enum.Parse<TestReportType>((string)jo["TestReportType"]);
            if (jo["ReceiverSource"] != null)
            {
                return new DirectMethodReportMetadata((string)jo["SenderSource"], (string)jo["ReceiverSource"], testReportType, (TimeSpan)jo["TolerancePeriod"]);
            }
            else
            {
                return new DirectMethodReportMetadata((string)jo["SenderSource"], testReportType, (TimeSpan)jo["TolerancePeriod"]);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
