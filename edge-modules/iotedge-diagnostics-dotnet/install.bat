ECHO OFF
dotnet build
dotnet publish
docker build --no-cache -t iotedgeforiiot.azurecr.io/azureiotedge-diagnostics:test --file ./docker/linux/arm32v7/Dockerfile --build-arg EXE_DIR=. ./bin/Debug/netcoreapp3.1/publish
docker push iotedgeforiiot.azurecr.io/azureiotedge-diagnostics:test