// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    public interface IDeliverableGeneratingStrategy<T>
    {
        T Next();
    }
}
