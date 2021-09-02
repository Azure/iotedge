# List Modules

The workload *List Modules* API is identical to the management *List Modules* API. This API is duplicated on the workload socket because the management socket is not available to all users.

## Request
```
GET /modules?api-version={version}
```

`version` must be at least `2018-06-28`.

## Response
```
200 OK

content-type: application/json
```

### Response body

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
