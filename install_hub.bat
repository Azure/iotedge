ECHO OFF
dotnet build
dotnet publish
docker build --no-cache -t iotedgeforiiot.azurecr.io/azureiotedge-hub:edgecheck --file ./edge-hub/core/src/Microsoft.Azure.Devices.Edge.Hub.Service/bin/Debug/netcoreapp3.1/publish/docker/linux/amd64/Dockerfile --build-arg EXE_DIR=. ./edge-hub/core/src/Microsoft.Azure.Devices.Edge.Hub.Service/bin/Debug/netcoreapp3.1/publish
docker push iotedgeforiiot.azurecr.io/azureiotedge-hub:edgecheck