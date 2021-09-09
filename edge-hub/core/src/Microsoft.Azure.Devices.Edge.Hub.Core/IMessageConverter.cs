// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public interface IMessageConverter
    {
    }

    public interface IMessageConverter<T> : IMessageConverter
    {
        IMessage ToMessage(T sourceMessage);

        T FromMessage(IMessage message);
    }
}
