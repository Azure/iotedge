# Test IoT Edge daemon API endpoint on dev machine

If you are a developer working on API endpoint changes, you will definitely want to test locally before committing any changes.  Follow below steps for IoT Edge daemon API endpoints testing.

### Pre-requisites
1. A dev machine is set up according to [devguide](https://github.com/Azure/iotedge/blob/master/edgelet/doc/devguide.md) in Linux.
2. Install Git on dev machine; refer details from [here](https://git-scm.com/download/linux).
3. Make sure to get the latest version of [IoT Edge source code](https://github.com/Azure/iotedge).

### Steps
Below are steps used to test API endpoints of iotedged running on Linux; and I will use [list_modules](https://github.com/Azure/iotedge/blob/master/edgelet/management/docs/ModuleApi.md#list_modules) as an example.

1. Create your IoT Edge device in Azure Portal.
2. Prepare config.yaml file.
	- Create config.yaml by cloning from `<iotedge repo local path>`/edgelet/contrib/config/linux to anywhere; e.g. /etc/iotedge/.
	- Open it by `sudo nano <config.yaml file path>`.
	- Update `hostname` in config.yaml.
	- Copy connection string from your IoT Edge device in Azure Portal created in step 1; and update `device_connection_string` in config.yaml.
    - You need to change to use http instead of unix socket for management and workload URI in config.yaml.  The reason using http is because API request will be validated to ensure it is coming from edgeAgent module by process id (pid) if it is run under unix socket; however for http, this validation is skipped.
	- Update `managment_uri` and `workload_uri` to `http://172.17.0.1:8080` and `http://172.17.0.1:8081` respectively in both "connect" and "listen" sections.  By default, docker bridge network will use 172.17.0.1.
3. Create folder for home directory (`homedir`) defined in config.yaml; default home directory path is `/var/lib/iotedge`.
4. Run iotedged by `cargo run -p iotedged -- -c <config.yaml file path>`.
5. If you see any exception complaining about permission denied for workload socket/management socket, you need to run `chmod 666` on that socket file.

**Permssion denied when start iotedged:**
```
Unhandled Exception: System.AggregateException: One or more errors occurred. (Permission denied /var/lib/iotedge/workload.sock) ---> System.Net.Internals.SocketExceptionFactory+ExtendedSocketException: Permission denied /var/lib/iotedge/workload.sock
   at System.Net.Sockets.Socket.DoConnect(EndPoint endPointSnapshot, SocketAddress socketAddress)
   at System.Net.Sockets.Socket.Connect(EndPoint remoteEP)
   at System.Net.Sockets.Socket.UnsafeBeginConnect(EndPoint remoteEP, AsyncCallback callback, Object state, Boolean flowContext)
   at System.Net.Sockets.Socket.BeginConnect(EndPoint remoteEP, AsyncCallback callback, Object state)
   at System.Net.Sockets.Socket.ConnectAsync(EndPoint remoteEP)
   at Microsoft.Azure.Devices.Edge.Util.Uds.HttpUdsMessageHandler.GetConnectedSocketAsync() in /home/vsts/work/1/s/edge-util/src/Microsoft.Azure.Devices.Edge.Util/uds/HttpUdsMessageHandler.cs:line 48
   at Microsoft.Azure.Devices.Edge.Util.Uds.HttpUdsMessageHandler.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) in /home/vsts/work/1/s/edge-util/src/Microsoft.Azure.Devices.Edge.Util/uds/HttpUdsMessageHandler.cs:line 24
   at System.Net.Http.HttpClient.FinishSendAsyncUnbuffered(Task`1 sendTask, HttpRequestMessage request, CancellationTokenSource cts, Boolean disposeCts)
   at Microsoft.Azure.Devices.Edge.Util.Edged.GeneratedCode.HttpWorkloadClient.CreateServerCertificateAsync(String api_version, String name, String genid, ServerCertificateRequest request, CancellationToken cancellationToken) in /home/vsts/work/1/s/edge-util/src/Microsoft.Azure.Devices.Edge.Util/edged/generatedCode/HttpWorkloadClient.cs:line 583
   at Microsoft.Azure.Devices.Edge.Util.Edged.WorkloadClient.Execute[T](Func`1 func, String operation)
   at Microsoft.Azure.Devices.Edge.Util.Edged.WorkloadClient.CreateServerCertificateAsync(String hostname, DateTime expiration) in /home/vsts/work/1/s/edge-util/src/Microsoft.Azure.Devices.Edge.Util/edged/WorkloadClient.cs:line 42
   at Microsoft.Azure.Devices.Edge.Util.CertificateHelper.GetServerCertificatesFromEdgelet(Uri workloadUri, String workloadApiVersion, String moduleId, String moduleGenerationId, String edgeHubHostname, DateTime expiration) in /home/vsts/work/1/s/edge-util/src/Microsoft.Azure.Devices.Edge.Util/CertificateHelper.cs:line 207
   at Microsoft.Azure.Devices.Edge.Hub.Service.Program.MainAsync(IConfigurationRoot configuration) in /home/vsts/work/1/s/edge-hub/src/Microsoft.Azure.Devices.Edge.Hub.Service/Program.cs:line 62
   --- End of inner exception stack trace ---
   at System.Threading.Tasks.Task`1.GetResultCore(Boolean waitCompletionNotification)
   at Microsoft.Azure.Devices.Edge.Hub.Service.Program.Main() in /home/vsts/work/1/s/edge-hub/src/Microsoft.Azure.Devices.Edge.Hub.Service/Program.cs:line 32
```
 
6. Once iotedged can successfully start, you should see similar output as below.

**Output from iotedged:**
```
    Finished dev [unoptimized + debuginfo] target(s) in 0.21s
     Running `/home/iotedgeuser/git/iotedge/edgelet/target/debug/iotedged -c /etc/iotedge/config.yaml`
2018-10-11T16:29:01Z [INFO] - Starting Azure IoT Edge Security Daemon
2018-10-11T16:29:01Z [INFO] - Version - 0.1.0
2018-10-11T16:29:01Z [INFO] - Using config file: /etc/iotedge/config.yaml
2018-10-11T16:29:01Z [INFO] - Using runtime network id azure-iot-edge
2018-10-11T16:29:01Z [INFO] - Initializing the module runtime...
2018-10-11T16:29:01Z [INFO] - Finished initializing the module runtime.
2018-10-11T16:29:01Z [INFO] - Configuring /var/lib/iotedge as the home directory.
2018-10-11T16:29:01Z [INFO] - Configuring certificates...
2018-10-11T16:29:01Z [INFO] - Transparent gateway certificates not found, operating in quick start mode...
2018-10-11T16:29:01Z [INFO] - Finished configuring certificates.
2018-10-11T16:29:01Z [INFO] - Initializing hsm...
2018-10-11T16:29:01Z [INFO] - Finished initializing hsm.
2018-10-11T16:29:01Z [INFO] - Detecting if configuration file has changed...
2018-10-11T16:29:01Z [INFO] - No change to configuration file detected.
2018-10-11T16:29:01Z [INFO] - Obtaining workload CA succeeded.
2018-10-11T16:29:01Z [INFO] - Provisioning edge device...
2018-10-11T16:29:01Z [INFO] - Manually provisioning device "devvm" in hub "xxx-iothub.azure-devices.net"
2018-10-11T16:29:01Z [INFO] - Finished provisioning edge device.
2018-10-11T16:29:01Z [INFO] - Starting management API...
2018-10-11T16:29:01Z [INFO] - Starting workload API...
2018-10-11T16:29:01Z [INFO] - Starting watchdog with 60 second frequency...
2018-10-11T16:29:01Z [INFO] - Listening on http://172.17.0.1:8080/ with 1 thread for management API.
2018-10-11T16:29:01Z [INFO] - Listening on http://172.17.0.1:8081/ with 1 thread for workload API.
2018-10-11T16:29:01Z [INFO] - Checking edge runtime status
2018-10-11T16:29:01Z [INFO] - Edge runtime status is stopped, starting module now...
2018-10-11T16:29:03Z [INFO] - [mgmt] - - - [2018-10-11 16:29:03.255889269 UTC] "GET /systeminfo?api-version=2018-06-28 HTTP/1.1" 200 OK 60 "-" "-" pid(any)
2018-10-11T16:29:03Z [INFO] - [work] - - - [2018-10-11 16:29:03.452050679 UTC] "POST /modules/%24edgeAgent/genid/636748070676263768/decrypt?api-version=2018-06-28 HTTP/1.1" 200 OK 1236 "-" "-" pid(any)
2018-10-11T16:29:03Z [INFO] - [work] - - - [2018-10-11 16:29:03.543401118 UTC] "POST /modules/%24edgeAgent/genid/636748070676263768/sign?api-version=2018-06-28 HTTP/1.1" 200 OK 57 "-" "-" pid(any)
2018-10-11T16:29:03Z [INFO] - [mgmt] - - - [2018-10-11 16:29:03.666959495 UTC] "GET /modules?api-version=2018-06-28 HTTP/1.1" 200 OK 1581 "-" "-" pid(any)
2018-10-11T16:29:04Z [INFO] - [work] - - - [2018-10-11 16:29:04.627120292 UTC] "POST /modules/%24edgeAgent/genid/636748070676263768/encrypt?api-version=2018-06-28 HTTP/1.1" 200 OK 1261 "-" "-" pid(any)
```

7. You can use `curl` to send request to API endpoint for testing.  For example, test [list_modules](https://github.com/Azure/iotedge/blob/master/edgelet/management/docs/ModuleApi.md#list_modules) endpoint:

    `curl -v http://172.17.0.1:8080/modules/?api-version=2018-06-28`

**Sample output for list modules endpoint:**
```
*   Trying 172.17.0.1...
* TCP_NODELAY set
* Connected to 172.17.0.1 (172.17.0.1) port 8080 (#0)
> GET /modules/?api-version=2018-06-28 HTTP/1.1
> Host: 172.17.0.1:8080
> User-Agent: curl/7.58.0
> Accept: */*
>
< HTTP/1.1 200 OK
< content-type: application/json
< content-length: 1584
< date: Thu, 11 Oct 2018 16:34:33 GMT
<
{"modules":[{"id":"id","name":"tempSensor","type":"docker","config":{"settings":{"createOptions":{"Labels":{"net.azure-devices.edge.owner":"Microsoft.Azure.Devices.Edge.Agent"}},"image":"mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0","imageHash":"sha256:e6834061c9acb6f0d43606badc744faf796687c9c136f3291646f15d3ae401b6"},"env":[]},"status":{"startTime":"2018-10-11T16:34:33.265723085+00:00","exitStatus":{"exitTime":"2018-10-11T16:29:12.591791205+00:00","statusCode":"0"},"runtimeStatus":{"status":"running","description":"running"}}},{"id":"id","name":"edgeHub","type":"docker","config":{"settings":{"createOptions":{"Labels":{"net.azure-devices.edge.owner":"Microsoft.Azure.Devices.Edge.Agent"}},"image":"mcr.microsoft.com/azureiotedge-hub:1.0","imageHash":"sha256:7b6717b3909038ab7c6f87845d5d8b7824e0f23e47f2c1147d697a5f55d89d41"},"env":[]},"status":{"startTime":"2018-10-11T16:34:32.345451350+00:00","exitStatus":{"exitTime":"2018-10-11T16:29:23.779925473+00:00","statusCode":"0"},"runtimeStatus":{"sta* Connection #0 to host 172.17.0.1 left intact
tus":"running","description":"running"}}},{"id":"id","name":"edgeAgent","type":"docker","config":{"settings":{"createOptions":{"Labels":{"net.azure-devices.edge.owner":"Microsoft.Azure.Devices.Edge.Agent"}},"image":"mcr.microsoft.com/azureiotedge-agent:1.0","imageHash":"sha256:672d449fb374dc6074cacbd9e279ee78fbdc15c39db7d0371694f64757bc7320"},"env":[]},"status":{"startTime":"2018-10-11T16:34:28.377072702+00:00","exitStatus":{"exitTime":"2018-10-11T16:29:24.917448244+00:00","statusCode":"0"},"runtimeStatus":{"status":"running","description":"running"}}}]}
```

8. You should follow the structure of requests mentioned in Swagger-generated API documentation.
    - [Management Identity API](https://github.com/Azure/iotedge/blob/master/edgelet/management/docs/IdentityApi.md)
    - [Management Module API](https://github.com/Azure/iotedge/blob/master/edgelet/management/docs/ModuleApi.md)
    - [Workload API](https://github.com/Azure/iotedge/blob/master/edgelet/workload/docs/WorkloadApi.md)

### How to construct API request

1. Let's take [create_module](https://github.com/Azure/iotedge/blob/master/edgelet/management/docs/ModuleApi.md#create_module) endpoint as an example.
2. On the top of this page, you will find which HTTP request method (Get, Post, Delete, Put) and path should be used for each API endpoint.
3. You can see required parameters and HTTP request headers for create_module endpoint.  The required parameters are `api_version:String` and `module:ModuleSpec`.
    - `api_version` should be defined in request URI.
    - `module` has a `config` field and it has a `settings` field; which is same as the settings value you can find from review deployment page of 'Set Modules' of an IoT Edge device in Azure Portal.

    For example:
    ```
        "systemModules": {
            "edgeAgent": {
                "type": "docker",
                "settings": {
                    "image": "mcr.microsoft.com/azureiotedge-agent:1.0",
                    "createOptions": ""
                }
            },
            "edgeHub": {
                "type": "docker",
                "settings": {
                    "image": "mcr.microsoft.com/azureiotedge-hub:1.0",
                    "createOptions": "{\"HostConfig\":{\"PortBindings\":{\"8883/tcp\":[{\"HostPort\":\"8883\"}],\"443/tcp\":[{\"HostPort\":\"443\"}],\"5671/tcp\":[{\"HostPort\":\"5671\"}]}}}"
                },
                "status": "running",
                "restartPolicy": "always"
            }
        },
        "modules": {
            "tempSensor": {
                "type": "docker",
                "settings": {
                    "image": "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0",
                    "createOptions": ""
                },
                "version": "1.0",
                "status": "running",
                "restartPolicy": "always"
            }
        }
    ```
4. Here is the example of request for create_module endpoint:

    >curl -v -X POST  http://172.17.0.1:8080/modules/?api-version=2018-06-28 -H "Content-Type:application/json" -H "Accept:application/json" -d '{ "name": "create_module", "type": "docker", "config": { "settings": { "image":"mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0", "createOptions":{} } } }'

5. Here is another example of request for restart_module request:



    >curl -v -X POST http://172.17.0.1:8080/modules/edgeHub/restart/?api-version=2018-06-28