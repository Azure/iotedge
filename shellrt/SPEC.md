# shellrt Specification

**Until the API stabilizes, the `shellrt-api` should be used as the source of truth for valid message format.**

This document is an _informal specification_, acting as a quick overview of the protocol.

## Informal Spec

### Terminology:

- "shellrt server": an application which spawns a "shellrt plugin", sending it requests via stdin, and receiving output via stdout.
- "shellrt plugin": an application which is able to send/receive shellrt requests/responses

### Protocol Overview

Requests are accepted via a plugin's stdin.
Responses are returned via a plugin's stdout.
The plugin's stderr can be used to output logs / debug information. The server MUST NOT use stderr to communicate with the plugin.

Payloads are top-level JSON objects, with shellrt-api metadata inline as top-level keys with a leading `_` character. Payloads are not allowed to use top-level keys with a leading `_`, as it may result in a collision.

While most responses are "one-shot", with the plugin outputting JSON via stdout and immediately terminating, the "logs" method uses a "streaming" response, structured as follows:

- Just like "one-shot" responses, the plugin will output a well-formed JSON response indicating that the request has been received (along with any associated metadata, if required).
- The plugin will output a **single null byte ('\0')** to delimit the end of the "one-shot" response, and the start of streaming data
- The plugin will then output stream of _raw, unstructured data_ (i.e: raw bytes, which may or may not be valid UTF-8) over stdout.

### Error Handling

Error codes 0-99 are reserved for well-known errors (see below). Values of 100+ can be freely used for plugin specific errors.

`"details"` is an optional field, and may contain arbitrary JSON data.

#### Well-Known Errors

* 1 - Incompatible api version
* 2 - Invalid request

### Examples

#### Request

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

#### Successful Response

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

#### Error Response

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
