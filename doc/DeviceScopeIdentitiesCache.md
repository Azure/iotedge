# Device scope identities cache

For configuration settings, see [Environment variables](EnvironmentVariables.md).

## Module identities in the device scope cache

When an individual module lookup returns both the module and its matching parent
device, Edge Hub caches the parent before the module if the parent is not already
cached, so the authentication hierarchy can build the module's upstream
authentication chain. This uses the same response, without an additional device
lookup or a refresh interval change. Existing parents are left to device and bulk
refreshes to update.

A missing parent is not inferred: if the response omits it and it is not already
cached, the module can still lack an authentication chain. A later bulk refresh
can still remove identities omitted from its response. The parent existence
check and insertion are not atomic, so a concurrent parent update can still be
overwritten. A stale module response can also reinsert a previously removed
parent. This is a partial mitigation, not a guarantee against stale responses.
