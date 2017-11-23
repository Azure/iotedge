// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.MessageSources
{
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Newtonsoft.Json;

    class CustomMessageSource : BaseMessageSource
    {
        [JsonConstructor]
        CustomMessageSource(string source)
            : base(source)
        {
        }

        public static CustomMessageSource Create(string source)
        {
            Preconditions.CheckArgument(!string.IsNullOrWhiteSpace(source), "Source cannot be null or empty");
            source = source?.Trim()?.TrimEnd('*') ?? string.Empty;
            source = source.Length > 1 ? source.TrimEnd('/') : source;
            return new CustomMessageSource(source);
        }
    }
}
