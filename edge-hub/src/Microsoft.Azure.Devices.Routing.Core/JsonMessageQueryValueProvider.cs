// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Newtonsoft.Json.Linq;

    public class JsonMessageQueryValueProvider : IMessageQueryValueProvider
    {
        readonly Lazy<JObject> parsedJObject;

        public JsonMessageQueryValueProvider(Encoding encoding, byte[] bytes)
        {
            Preconditions.CheckNotNull(bytes);

            this.parsedJObject = new Lazy<JObject>(
                () => JObject.Parse(encoding.GetString(bytes)));
        }

        public QueryValue GetQueryValue(string queryString)
        {
            List<JToken> jsonTokens = this.parsedJObject.Value.SelectTokens(queryString).ToList();

            if (!jsonTokens.Any())
            {
                return QueryValue.Null;
            }
            else if (jsonTokens.Count > 1)
            {
                return new QueryValue(jsonTokens, QueryValueType.Object);
            }

            JToken firstJsonToken = jsonTokens.First();
            var jsonValue = firstJsonToken as JValue;

            if (jsonValue == null)
            {
                // When jtoken is not a value but an object (non-leaf)
                return new QueryValue(firstJsonToken, QueryValueType.Object);
            }
            else
            {
                // Leaf node of Json tree
                return QueryValue.Create(jsonValue);
            }
        }
    }
}
