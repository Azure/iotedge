// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.MessageSources
{
    public static class MessageSourceExtensions
    {
        public static bool IsTelemetry(this IMessageSource messageSource) => messageSource != null
                                                                             && messageSource is BaseMessageSource baseMessageSource
                                                                             && baseMessageSource.Source.StartsWith("/messages/");
    }
}
