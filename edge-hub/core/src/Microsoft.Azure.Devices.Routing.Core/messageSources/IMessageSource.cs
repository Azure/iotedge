// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.MessageSources
{
    public interface IMessageSource
    {
        bool Match(IMessageSource messageSource);
    }
}
