// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public interface IMessageConverter
    {
    }

    public interface IMessageConverter<T> : IMessageConverter
    {
        T FromMessage(IMessage message);

        IMessage ToMessage(T sourceMessage);
    }
}
