// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
