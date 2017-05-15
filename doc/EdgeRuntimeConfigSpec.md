Specification for Edge Runtime Configuration
============================================

Goal
----

This document serves as a specification of configuration data that an Edge
Module developer would use to obtain information about Edge Runtime.
This configuration data essentially can be used by all modules to communicate
with the Edge Hub and gain information about the underlying host OS.

**Note:** This document is a work in progress and is subject to change as the
product evolves. Details about security credentials are still being spec'ed
and will likely be appended to the data set below.

Configuration Data
------------------

The sections listed below contain data as Key Value (KV) pairs.
These have been categorized per domain and list several examples of the types
of values that these KV pairs can take.

>### Edge Host Runtime

| Key        | Value Type | Examples                     |
| ----------:|:----------:|:---------------------------- |
| platform   | string     | "Windows", "Linux"           |
| osName     | string     | "Windows", "Ubuntu", "CentOS"|
| osVersion  | string     | "10.0.15063", "16.04"        |

>### Edge Hub

| Key        | Value Type | Examples                     |
| ----------:|:----------:|:---------------------------- |
| hostName   | string     | "edge-iot-hub.domain.net"    |
| ip         | string     | "1.2.3.4"                    |
| mqttPort   | integer    | 8883                         |
| amqpPort   | integer    | 5672                         |
| httpPort   | integer    | 443                          |

>### Putting It All Together
The following is a cumulative JSON representation of the data using sample data:

```JSON
{
    "schemaVersion": "1.0.0",
    "edgeHub": {
        "hostName": "edge-iot-hub.domain.net",
        "ip": "1.2.3.4",
        "ports": {
            "mqtt": 8883,
            "http": 443,
            "amqp": 5672
        }
    },
    "edgeHostRuntime": {
        "platform": "Linux",
        "os": {
            "name": "Ubuntu",
            "version: "16.04"
        }
    }
}
```

Data Access Mechanism
---------------------

The followings section(s) list how this configuration data will be made
available to Edge Modules at runtime. The access mechanism to the configuration
data depends on the underlying platform that the module executes in.

>### Docker Containers
>
>The above JSON data will be made available to all modules as a read only
volume mounted file within their container. The path at which this will be
available will be declared in an environment variable listed below:
```bash
AZEDGE_CONFIG=/etc/azedge/config.json
```
>*Note:*It will be the responsibility of the Edge Agent to perform this action.