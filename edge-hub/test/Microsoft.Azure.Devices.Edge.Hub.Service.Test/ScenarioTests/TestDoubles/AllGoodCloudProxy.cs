// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;

    public class AllGoodCloudProxy : ICloudProxy
    {
        public bool IsActive => true;

        public virtual Task<bool> CloseAsync() => Task.FromResult(true);
        public virtual Task<IMessage> GetTwinAsync() => Task.FromResult(new EdgeMessage(new byte[0], new Dictionary<string, string>(), new Dictionary<string, string>()) as IMessage);
        public virtual Task<bool> OpenAsync() => Task.FromResult(true);
        public virtual Task RemoveCallMethodAsync() => Task.CompletedTask;
        public virtual Task RemoveDesiredPropertyUpdatesAsync() => Task.CompletedTask;
        public virtual Task SendFeedbackMessageAsync(string messageId, FeedbackStatus feedbackStatus) => Task.CompletedTask;
        public virtual Task SendMessageAsync(IMessage message) => Task.CompletedTask;
        public virtual Task SendMessageBatchAsync(IEnumerable<IMessage> inputMessages) => Task.CompletedTask;
        public virtual Task SetupCallMethodAsync() => Task.CompletedTask;
        public virtual Task SetupDesiredPropertyUpdatesAsync() => Task.CompletedTask;
        public virtual Task StartListening() => Task.CompletedTask;
        public virtual Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage) => Task.CompletedTask;
    }
}
