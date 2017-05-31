// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.Azure.Devices.Edge.Util;

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
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
