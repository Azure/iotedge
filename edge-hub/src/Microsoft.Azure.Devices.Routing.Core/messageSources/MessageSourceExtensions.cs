// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.MessageSources
{
    public static class MessageSourceExtensions
    {
        public static bool IsTelemetry(this IMessageSource messageSource) => messageSource != null && 
            (messageSource is TelemetryMessageSource || messageSource is ModuleMessageSource);
    }
}