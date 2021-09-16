# Encryption APIs

## Encrypt

This API is only available to the module specified in the request URI. All other callers will receive `403 Forbidden`.

### Request
```
POST /modules/{module-id}/genid/{gen-id}/encrypt?api-version={version}

content-type: application/json
```

`version` must be at least `2018-06-28`.

#### Request body
```
{
    "plaintext": "base64-encoded string",
    "initializationVector": "base64-encoded string"
}
```

### Response
```
200 OK

content-type: application/json
```

#### Response body
```
{
    "ciphertext": "base64-encoded string",
}
```

---

## Decrypt

This API is only available to the module specified in the request URI. All other callers will receive `403 Forbidden`.

### Request
```
POST /modules/{module-id}/genid/{gen-id}/decrypt?api-version={version}

content-type: application/json
```

`version` must be at least `2018-06-28`.

#### Request body
```
{
    "ciphertext": "base64-encoded string",
    "initializationVector": "base64-encoded string"
}
```

### Response
```
200 OK

content-type: application/json
```

#### Response body
```
{
    "plaintext": "base64-encoded string",
}
```

---

## Sign

This API is only available to the module specified in the request URI. All other callers will receive `403 Forbidden`.

### Request
```
POST /modules/{module-id}/genid/{gen-id}/sign?api-version={version}

content-type: application/json
```

`version` must be at least `2018-06-28`.

#### Request body
```
{
    "data": "base64-encoded string"
}
```

### Response
```
200 OK

content-type: application/json
```

#### Response body
```
{
    "digest": "base64-encoded string",
}
```
