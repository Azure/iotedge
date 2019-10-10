namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    public interface IMessageGeneratingStrategy
    {
        Routing.Core.Message Next();
    }
}
