// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.MessageSources
{
    public class TwinChangeEventMessageSource : BaseMessageSource
    {
        TwinChangeEventMessageSource()
            : base("/twinChangeNotifications")
        {
        }

        public static TwinChangeEventMessageSource Instance { get; } = new TwinChangeEventMessageSource();
    }
}