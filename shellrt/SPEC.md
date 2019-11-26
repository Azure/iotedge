# shellrt Specification

Until things stabilize, the `shellrt-api` crate will be the source of truth for valid message format.

## Informal Spec

Requests are accepted via stdin.
Responses are returned via stdout.

Payloads are top-level JSON objects, with shellrt-api metadata inline as top-level keys with a leading `_` character. Payloads are not allowed to use top-level keys with a leading `_`, as it may result in a collision.

### Request

```json
{
    "_version": "0.1.0",
    "_type": "<action type>",
    "req_field_1": "foo",
    "req_field_2": {
        "bar": "baz"
    },
}
```

### Response

On success:

```json
{
    "_version": "0.1.0",
    "_status": "ok",
    "res_field_1": "foo",
    "res_field_2": {
        "bar": "baz"
    },
}
```

On error:

```json
{
    "_version": "0.1.0",
    "_status": "err",
    "code": 123,
    "message": "failed to reticulate splines",
    "details": {
        "foo": "bar",
        "baz": 1337
    }
}
```

Error codes 0-99 are reserved for well-known errors (see below). Values of 100+ can be freely used for plugin specific errors.

`"details"` is an optional field, and may contain arbitrary JSON data.

## Well-Known Errors

* 1 - Incompatible api version
* 2 - Invalid request
