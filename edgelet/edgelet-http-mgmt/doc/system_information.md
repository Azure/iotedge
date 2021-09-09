# System Information

## Get System Information

### Request
```
GET /systeminfo?api-version={version}
```

`version` must be at least `2018-06-28`.

### Response
```
200 OK

content-type: application/json
```

#### Response body
```
{
    "osType": "string",
    "architecture": "string",
    "version": "string",
    "provisioning": {
        "type": "string",
        "dynamicReprovisioning": bool,
        "alwaysReprovisionOnStartup": bool
    },
    "server_version": "string",
    "kernel_version": "string",
    "operating_system": "string",
    "cpus": int,
    "virtualized": "string"
}
```

---

## Get System Resources

### Request
```
GET /systeminfo/resources?api-version={version}
```

`version` must be at least `2019-11-05`.

### Response
```
200 OK

content-type: application/json
```

#### Response body
```
{
    "host_uptime": int,
    "process_uptime": int,
    "used_cpu": float,
    "used_ram": int,
    "total_ram": int,
    "disks": [
        {
            "name": "string",
            "available_space": int,
            "total_space": int,
            "file_system": "string",
            "file_type": "string"
        }
    ],
    "docker_stats": "json"
}
```

---

## Get Support Bundle

### Request
```
GET /systeminfo/resources?api-version={version}
    &since={time}
    &until={time}
    &iothub_hostname={string}
    &edge_runtime_only={bool}
```

`version` must be at least `2020-07-07`.

### Response
```
200 OK

content-type: application/zip
```

Zip file.
