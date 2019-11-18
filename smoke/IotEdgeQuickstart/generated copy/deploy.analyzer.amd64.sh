dotnet publish
docker build -t edgebuilds.azurecr.io/microsoft/azureiotedge-analyzer:debug-linux-amd64 --file /home/andrew/Desktop/tmp/iotedge/edge-modules/Analyzer/docker/linux/amd64/Dockerfile /home/andrew/Desktop/tmp/iotedge/edge-modules/Analyzer/bin/Debug/netcoreapp2.1/publish
docker push edgebuilds.azurecr.io/microsoft/azureiotedge-analyzer:debug-linux-amd64