# Trust Bundles API

## Get Trust Bundle

### Request
```
GET /trust-bundle?api-version={version}
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
    "certificate": "string"
}
```