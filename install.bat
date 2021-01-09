ECHO OFF
dotnet build
dotnet publish
docker build --no-cache -t huguesb.azurecr.io/agent_test:16 --file ./edge-agent/src/Microsoft.Azure.Devices.Edge.Agent.Service/bin/Debug/netcoreapp3.1/publish/docker/linux/amd64/Dockerfile --build-arg EXE_DIR=. ./edge-agent/src/Microsoft.Azure.Devices.Edge.Agent.Service/bin/Debug/netcoreapp3.1/publish
docker push huguesb.azurecr.io/agent_test:16