echo "start"
./scripts/linux/buildBranch.sh -c Release --no-rocksdb-bin
./scripts/linux/consolidate-build-artifacts.sh --build-binaries-dir './target' --artifact-name 'edge-hub'
./scripts/linux/buildImage.sh -r "hugues.azurecr.io" -i "edge-hub" -n "microsoft" -P "edge-hub" -v "1.0" --bin-dir './target'
./scripts/linux/buildImage.sh -r "hugues.azurecr.io" -i "azureiotedge-simulated-temperature-sensor" -n "microsoft" -P "SimulatedTemperatureSensor" -v "1.0" --bin-dir './target'
#sudo apt-get update; \
#  sudo apt-get install -y apt-transport-https && \
#  sudo apt-get update && \
#  sudo apt-get install -y dotnet-sdk-3.1
# 
#echo "finish updating"
#./scripts/linux/buildBranch.sh -c Release --no-rocksdb-bin
#echo "finish build branch"
#./scripts/linux/cross-platform-rust-build.sh --os alpine --arch amd64 --build-path mqtt/mqttd
#echo "finish build mqtt"
#./scripts/linux/cross-platform-rust-build.sh --os alpine --arch amd64 --build-path edge-hub/watchdog
#
#echo "finish build watchdog"
#./scripts/linux/consolidate-build-artifacts.sh --build-binaries-dir './target' --artifact-name 'edge-hub'
#echo "finish build artifacts"
#scripts/linux/buildImage.sh -r "hugues.azurecr.io" -i "edge-hub" -n "microsoft" -P "edge-hub" -v "1.0" --bin-dir './target'
