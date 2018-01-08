using Xunit;

namespace Microsoft.Azure.Devices.Edge.SimulateEdgeDevice.Test
{
    public class SimulateEdgeDeviceTest
    {
        [Fact(Skip = "Not implemented yet")]
        [Deploy]
        public void Run()
        {
            // register a unique edge device on an IoT hub
            // (fail if any previous deployment tries to overwrite this edge device's config)
            // install iotedgectl (via pip or 
            // run `iotedgectl setup ...`
            // run `iotedgectl start`
            // verify: run `docker ps` to confirm edge agent is running
            // verify: ping the edge device to confirm it is connected to the cloud
            // update edge device's config to include tempSensor module
            // verify: run `docker ps` to confirm tempSensor is running
            // verify: monitor Event Hub to confirm telemetry is flowing from device to cloud
        }
    }
}
