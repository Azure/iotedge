// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Azure.Devices.Edge.Util;

    public class MessageConverterProvider : IMessageConverterProvider
    {
        readonly IDictionary<Type, IMessageConverter> converters;

        public MessageConverterProvider(IDictionary<Type, IMessageConverter> converters)
        {
            this.converters = Preconditions.CheckNotNull(converters, nameof(converters));
        }

        public IMessageConverter<T> Get<T>()
        {
            return this.converters[typeof(T)] as IMessageConverter<T>;
        }
    }
}
