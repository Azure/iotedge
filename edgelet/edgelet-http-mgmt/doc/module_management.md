# Module Management

## Create Module

This API is only available to `edgeAgent`. All other callers will receive `403 Forbidden`.

### Request
```
POST /modules?api-version={version}

content-type: application/json
```

`version` must be at least `2018-06-28`.

#### Request body
```
{
    "name": "string",
    "type": "string",
    "config": {
        "settings": json,
        "env": [
            {
                "key": "string",
                "value": "string,
            }
        ]
    },
    "imagePullPolicy": "string"
}
```

`imagePullPolicy` may be either `"on-create"` or `"never"`. It is optional and defaults to `"on-create"` if omitted.

### Response
```
201 Created

content-type: application/json
```

#### Response body
```
{
    "id": "string",
    "name": "string",
    "type": "string",
    "config": {
        "settings": json,
        "env": [
            {
                "key": "string",
                "value": "string,
            }
        ]
    },
    "status": {
        "runtimeStatus": {
            "status": "stopped"
        }
    }
}
```

---
