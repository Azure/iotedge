ECHO OFF
dotnet build
dotnet publish
docker build --no-cache -t huguesb.azurecr.io/azureiotedge-agent:test --file ./edge-agent/src/Microsoft.Azure.Devices.Edge.agent.Service/bin/Debug/netcoreapp3.1/publish/docker/linux/amd64/Dockerfile --build-arg EXE_DIR=. ./edge-agent/src/Microsoft.Azure.Devices.Edge.agent.Service/bin/Debug/netcoreapp3.1/publish
docker push huguesb.azurecr.io/azureiotedge-agent:test