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

## List Modules

### Request
```
GET /modules?api-version={version}
```

`version` must be at least `2018-06-28`.

### Response
```
200 OK

content-type: application/json
```

#### Response body

The response body contains an array of modules.

```
{
    modules: [
        {
            "id": "id",
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
                "startTime": "string",
                "exitStatus: {
                    "exitTime": "string",
                    "statusCode": "string"
                },
                "runtimeStatus": {
                    "status": "string",
                    "description": "string"
                }
            }
        }
    ]
}
```

---

## Delete Module

This API is only available to `edgeAgent`. All other callers will receive `403 Forbidden`.

### Request
```
DELETE /modules/{module-id}?api-version={version}
```

`version` must be at least `2018-06-28`.

### Response
```
204 No Content
```

---

## Get Module

### Request
```
GET /modules/{module-id}?api-version={version}
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
    "id": "id",
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
        "startTime": "string",
        "exitStatus: {
            "exitTime": "string",
            "statusCode": "string"
        },
        "runtimeStatus": {
            "status": "string",
            "description": "string"
        }
    }
}
```

---

## Update Module

This API is only available to `edgeAgent`. All other callers will receive `403 Forbidden`.

### Request
```
PUT /modules/{module-id}?api-version={version}&start={bool}
```

`start` controls whether a module should be started after update. If not provided, it defaults to `false`.

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
200 OK

content-type: application/json
```

#### Response body
```
{
    "id": "id",
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
        "startTime": "string",
        "exitStatus: {
            "exitTime": "string",
            "statusCode": "string"
        },
        "runtimeStatus": {
            "status": "string",
            "description": "string"
        }
    }
}
```
