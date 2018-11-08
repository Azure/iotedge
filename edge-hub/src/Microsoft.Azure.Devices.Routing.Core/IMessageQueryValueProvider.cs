// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Globalization;
    using System.Text;

    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public interface IMessageQueryValueProvider
    {
        QueryValue GetQueryValue(string queryString);
    }

    public static class MessageQueryValueProviderFactory
    {
        public static IMessageQueryValueProvider Create(string contentType, Encoding encoding, byte[] bytes)
        {
            if (string.Equals(contentType, Constants.SystemPropertyValues.ContentType.Json, StringComparison.OrdinalIgnoreCase))
            {
                return new JsonMessageQueryValueProvider(encoding, bytes);
            }

            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Content type '{0}' is not supported.", contentType));
        }
    }
}
