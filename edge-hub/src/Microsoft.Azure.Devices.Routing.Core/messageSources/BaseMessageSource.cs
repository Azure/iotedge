// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.MessageSources
{
    using System;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class BaseMessageSource : IMessageSource
    {
        protected BaseMessageSource(string source)
        {
            this.Source = AppendSingleTailingSlash(Preconditions.CheckNotNull(source));
        }

        protected string Source { get; }
        
        public virtual bool Match(IMessageSource messageSource)
        {
            Preconditions.CheckNotNull(messageSource, nameof(messageSource));
            var baseMessageSource = messageSource as BaseMessageSource;
            return baseMessageSource?.Source != null &&
                AppendSingleTailingSlash(baseMessageSource.Source).StartsWith(this.Source, StringComparison.OrdinalIgnoreCase);
        }

        static string AppendSingleTailingSlash(string value) => value.Trim().TrimEnd('/') + "/";

        public override bool Equals(object obj)
        {
            var baseMessageSource = obj as BaseMessageSource;
            return baseMessageSource?.Source != null &&
                AppendSingleTailingSlash(baseMessageSource.Source).Equals(this.Source, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + this.Source.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => this.GetType().Name;
    }    
}