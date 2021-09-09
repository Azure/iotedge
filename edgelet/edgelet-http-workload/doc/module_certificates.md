# Module Certificate APIs

## Create Identity Certificate

This API is only available to the module specified in the request URI. All other callers will receive `403 Forbidden`.

### Request
```
POST /modules/{module-id}/certificate/identity?api-version={version}
```

`version` must be at least `2018-06-28`.

### Response
```
201 CREATED

content-type: application/json
```

#### Response body
```
{
    "privateKey": {
        "type: "key",
        "bytes": "string"
    },
    "certificate": "string",
    "expiration": "string"
}
```

---

## Create Server Certificate

This API is only available to the module specified in the request URI. All other callers will receive `403 Forbidden`.

### Request
```
POST /modules/{module-id}/genid/{gen-id}/certificate/server?api-version={version}

content-type: application/json
```

`version` must be at least `2018-06-28`.

#### Request body
```
{
    "commonName": "string"
}
```

### Response
```
201 CREATED

content-type: application/json
```

#### Response body
```
{
    "privateKey": {
        "type: "key",
        "bytes": "string"
    },
    "certificate": "string",
    "expiration": "string"
}
```
