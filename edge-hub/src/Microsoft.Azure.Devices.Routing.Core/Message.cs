// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;
    using Newtonsoft.Json;
    using SystemPropertiesList = SystemProperties;

    public class Message : IMessage
    {
        readonly Lazy<IMessageQueryValueProvider> messageQueryProvider;

        public Message(IMessageSource messageSource, byte[] body, IDictionary<string, string> properties)
            : this(messageSource, body, properties, new Dictionary<string, string>())
        {
        }

        public Message(IMessageSource messageSource, byte[] body, IDictionary<string, string> properties, long offset)
            : this(messageSource, body, properties, new Dictionary<string, string>(), offset)
        {
        }

        public Message(IMessageSource messageSource, byte[] body, IDictionary<string, string> properties, IDictionary<string, string> systemProperties)
            : this(messageSource, body, properties, systemProperties, 0L)
        {
        }

        public Message(IMessageSource messageSource, byte[] body, IDictionary<string, string> properties, IDictionary<string, string> systemProperties, DateTime enqueuedTime, DateTime dequeuedTime)
            : this(messageSource, body, properties, systemProperties, 0L, enqueuedTime, dequeuedTime)
        {
        }

        public Message(IMessageSource messageSource, byte[] body, IDictionary<string, string> properties, IDictionary<string, string> systemProperties, long offset)
            : this(messageSource, body, properties, systemProperties, offset, DateTime.MinValue, DateTime.UtcNow)
        {
        }

        public Message(IMessageSource messageSource, byte[] body, IDictionary<string, string> properties, IDictionary<string, string> systemProperties, long offset, DateTime enqueuedTime, DateTime dequeuedTime)
            : this(
                messageSource,
                body,
                new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(Preconditions.CheckNotNull(properties), StringComparer.OrdinalIgnoreCase)) as IReadOnlyDictionary<string, string>,
                new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(Preconditions.CheckNotNull(systemProperties), StringComparer.OrdinalIgnoreCase)),
                offset,
                enqueuedTime,
                dequeuedTime)
        {
        }

        public Message(IMessageSource messageSource, byte[] body, IReadOnlyDictionary<string, string> properties, IReadOnlyDictionary<string, string> systemProperties, long offset, DateTime enqueuedTime, DateTime dequeuedTime)
        {
            this.MessageSource = messageSource;
            this.Body = Preconditions.CheckNotNull(body);
            this.Properties = Preconditions.CheckNotNull(properties);
            this.SystemProperties = Preconditions.CheckNotNull(systemProperties);
            this.Offset = offset;
            this.EnqueuedTime = enqueuedTime;
            this.DequeuedTime = dequeuedTime;

            this.messageQueryProvider = new Lazy<IMessageQueryValueProvider>(this.GetMessageQueryProvider);
        }

        [JsonConstructor]
        // ReSharper disable once UnusedMember.Local
        Message(CustomMessageSource messageSource, byte[] body, IDictionary<string, string> properties, IDictionary<string, string> systemProperties, long offset, DateTime enqueuedTime, DateTime dequeuedTime)
            : this((IMessageSource)messageSource, body, properties, systemProperties, offset, enqueuedTime, dequeuedTime)
        {
        }

        public IMessageSource MessageSource { get; }

        public byte[] Body { get; }

        public IReadOnlyDictionary<string, string> Properties { get; }

        public IReadOnlyDictionary<string, string> SystemProperties { get; }

        public long Offset { get; }

        public DateTime EnqueuedTime { get; }

        public DateTime DequeuedTime { get; }

        public QueryValue GetQueryValue(string queryString)
        {
            return this.messageQueryProvider.Value.GetQueryValue(queryString);
        }

        public bool Equals(Message other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.MessageSource.Equals(other.MessageSource) &&
                this.Offset == other.Offset &&
                this.Body.SequenceEqual(other.Body) &&
                this.Properties.Keys.Count() == other.Properties.Keys.Count() &&
                this.Properties.Keys.All(
                    key => other.Properties.ContainsKey(key) && Equals(this.Properties[key], other.Properties[key]) &&
                        this.SystemProperties.Keys.Count() == other.SystemProperties.Keys.Count() &&
                        this.SystemProperties.Keys.All(skey => other.SystemProperties.ContainsKey(skey) && Equals(this.SystemProperties[skey], other.SystemProperties[skey])));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj.GetType() == this.GetType() && this.Equals((Message)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)(this.Offset ^ (this.Offset >> 32));
                hash = hash * 31 + this.MessageSource.GetHashCode();
                hash = this.Body.Aggregate(hash, (acc, b) => acc * 31 + b);
                hash = this.Properties.Aggregate(hash, (acc, pair) => (acc * 31 + pair.Key.GetHashCode()) * 31 + pair.Value?.GetHashCode() ?? 0);
                hash = this.SystemProperties.Aggregate(hash, (acc, pair) => (acc * 31 + pair.Key.GetHashCode()) * 31 + pair.Value?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public long Size()
        {
            long size = 0L;
            size += this.Properties.Aggregate(0, (acc, pair) => (acc + pair.Key.Length + pair.Value?.Length ?? 0));
            size += this.SystemProperties.Aggregate(0, (acc, pair) => (acc + pair.Key.Length + pair.Value?.Length ?? 0));
            size += this.Body.Length;
            return size;
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        IMessageQueryValueProvider GetMessageQueryProvider()
        {
            Encoding messageEncoding = this.GetMessageEncoding();
            string contentType;

            if (this.SystemProperties.TryGetValue(SystemPropertiesList.ContentType, out contentType))
            {
                return MessageQueryValueProviderFactory.Create(contentType, messageEncoding, this.Body);
            }

            throw new InvalidOperationException("Content type is not specified in system properties.");
        }

        Encoding GetMessageEncoding()
        {
            string encodingPropertyValue;

            if (this.SystemProperties.TryGetValue(SystemPropertiesList.ContentEncoding, out encodingPropertyValue))
            {
                Encoding encoding;
                if (Constants.SystemPropertyValues.ContentEncodings.TryGetValue(encodingPropertyValue, out encoding))
                {
                    return encoding;
                }

                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Content encoding '{0}' is not supported.",
                        encodingPropertyValue));
            }

            throw new InvalidOperationException("Content encoding is not specified in system properties.");
        }
    }
}
