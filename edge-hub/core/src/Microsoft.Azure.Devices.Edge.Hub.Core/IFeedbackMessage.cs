// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public interface IFeedbackMessage : IMessage
    {
        FeedbackStatus FeedbackStatus { get; }
    }
}
