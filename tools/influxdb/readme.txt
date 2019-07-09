How to build influxdb docker image for Windows

For amd64
1. Go to the corresponding docker folder, e.g. influxdb/docker/windows/amd64
2. Download influxdb 1.7.7 windows x64 from https://portal.influxdata.com/downloads/; and extract it.
3. Copy influxd.exe from extracted files to docker folder.
4. Run command "docker build --no-cache -t edgebuilds.azurecr.io/influxdb:1.7.7-windows-amd64 .".
5. Run command "docker run --name influxdb <image hash>" (find <image hash> by running "docker images").
6. Run commnad "docker inspect influxdb" to find out its ip address.
7. Open a browser and go to "http://<influxdb ip address>:8086/query"; and you should see this message "{"error":"missing required parameter \"q\""}". This means influxdb is up and running.
8. Docker login to "edgebuilds.azurecr.io".
9. Run command "docker push edgebuilds.azurecr.io/influxdb:1.7.7-windows-x64".