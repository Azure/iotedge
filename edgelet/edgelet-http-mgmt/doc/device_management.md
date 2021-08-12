# Device Management

## Reprovision

This API is only available to `edgeAgent`. All other callers will receive `403 Forbidden`.

### Request
```
POST /device/reprovision?api-version={version}
```

`version` must be at least `2019-10-22`.

### Response
```
204 No Content
```

Reprovisioning will cause `aziot-edged` to restart.
