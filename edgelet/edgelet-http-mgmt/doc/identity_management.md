# Identity Management

## Create Identity

This API is only available to `edgeAgent`. All other callers will receive `403 Forbidden`.

### Request
```
POST /identities?api-version={version}

content-type: application/json
```

`version` must be at least `2018-06-28`.

#### Request body
```
{
    "moduleId": "string",
    "managedBy": "string"
}
```

`managedBy` is optional and defaults to `"iotedge"` if not provided.

### Response
```
200 OK

content-type: application/json
```

#### Response body
```
{
    "moduleId": "string",
    "managedBy": "string",
    "generationId": "string",
    "authType": "sas"
}
```

---

## List Identities

This API is only available to `edgeAgent`. All other callers will receive `403 Forbidden`.

### Request
```
GET /identities?api-version={version}
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
    "identities": [
        {
            "moduleId": "string",
            "managedBy": "string",
            "generationId": "string",
            "authType": "sas"
        }
    ]
}
```

---

## Update Identity

This API is only available to `edgeAgent`. All other callers will receive `403 Forbidden`.

### Request
```
PUT /identities/{module-id}?api-version={version}
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
    "moduleId": "string",
    "managedBy": "string",
    "generationId": "string",
    "authType": "sas"
}
```

---

## Delete Identity

This API is only available to `edgeAgent`. All other callers will receive `403 Forbidden`.

### Request
```
DELETE /identities/{module-id}?api-version={version}
```

`version` must be at least `2018-06-28`.

### Response
```
204 No Content
```
