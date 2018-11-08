// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.MessageSources
{
    using System;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class BaseMessageSource : IMessageSource
    {
        protected BaseMessageSource(string source)
        {
            this.Source = AppendSingleTrailingSlash(Preconditions.CheckNotNull(source));
        }

        public string Source { get; }

        public override bool Equals(object obj)
        {
            var baseMessageSource = obj as BaseMessageSource;
            return baseMessageSource?.Source != null &&
                   AppendSingleTrailingSlash(baseMessageSource.Source).Equals(this.Source, StringComparison.OrdinalIgnoreCase);
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

        public virtual bool Match(IMessageSource messageSource)
        {
            Preconditions.CheckNotNull(messageSource, nameof(messageSource));
            var baseMessageSource = messageSource as BaseMessageSource;
            return baseMessageSource?.Source != null &&
                   AppendSingleTrailingSlash(baseMessageSource.Source).StartsWith(this.Source, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString() => this.GetType().Name;

        static string AppendSingleTrailingSlash(string value) => value.Trim().TrimEnd('/') + "/";
    }
}
