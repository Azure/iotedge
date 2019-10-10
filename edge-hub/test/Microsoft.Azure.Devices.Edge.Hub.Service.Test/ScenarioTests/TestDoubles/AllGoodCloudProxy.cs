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
        public virtual Task RemoveCallMethodAsync() => Task.FromResult(true);
        public virtual Task RemoveDesiredPropertyUpdatesAsync() => Task.FromResult(true);
        public virtual Task SendFeedbackMessageAsync(string messageId, FeedbackStatus feedbackStatus) => Task.FromResult(true);
        public virtual Task SendMessageAsync(IMessage message) => Task.FromResult(true);
        public virtual Task SendMessageBatchAsync(IEnumerable<IMessage> inputMessages) => Task.FromResult(true);
        public virtual Task SetupCallMethodAsync() => Task.FromResult(true);
        public virtual Task SetupDesiredPropertyUpdatesAsync() => Task.FromResult(true);
        public virtual Task StartListening() => Task.FromResult(true);
        public virtual Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage) => Task.FromResult(true);
    }
}
