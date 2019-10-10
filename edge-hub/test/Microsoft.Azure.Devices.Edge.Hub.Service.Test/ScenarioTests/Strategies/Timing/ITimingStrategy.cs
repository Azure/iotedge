// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System.Threading.Tasks;

    public interface ITimingStrategy
    {
        Task DelayAsync();
    }
}
