# Device Management

## Reprovision

This API is only available to `edgeAgent`. All other callers will receive `403 Forbidden`.

### Request
```
POST /device/reprovision?api-version={version}
```

`version` must be at least `2019_10_22`.

### Response
```
200 OK
```

Reprovisioning will cause `aziot-edged` to restart.
