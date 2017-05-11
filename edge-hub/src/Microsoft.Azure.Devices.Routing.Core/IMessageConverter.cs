// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    public interface IMessageConverter<T>
    {
        IMessage ToMessage(T sourceMessage);

        T FromMessage(IMessage message);
    }
}