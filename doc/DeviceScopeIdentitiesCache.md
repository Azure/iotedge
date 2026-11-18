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

## Leaf device identities in the device scope cache

After a successful individual lookup of an enabled leaf device, Edge Hub can
recover its own missing gateway identity with an additional device lookup. With
nested support enabled, this uses `getDeviceAndModuleOnBehalfOf` without a module
ID, acting on behalf of the current Edge device. The gateway is cached only when
it is enabled, has the IoT Edge capability, and its `deviceScope` matches one of
the leaf's `parentScopes`. The hierarchy then attaches the leaf to the gateway.

This fallback does not replace a cached gateway or overwrite a gateway update
or removal that occurs while the lookup is in flight. Concurrent leaf refreshes
share the parent lookup, and successful, empty, rejected, and failed lookups all
use the configured identity refresh cooldown before another recovery attempt.
If recovery fails, the leaf identity remains cached, but no authentication chain
is synthesized.

No gateway recovery lookup is made when the leaf lookup fails or returns no
identity, the leaf is disabled or has no parent scopes, the gateway is already
cached, or the leaf already has an authentication chain. After successful
recovery, other leaves reuse the cached gateway. There is no background retry
loop: a later successful leaf refresh can retry failed recovery after the shared
cooldown, or recover a gateway that was subsequently removed from the cache.
The existing lookup behavior for unknown or out-of-scope leaves is unchanged.
These counts refer to logical lookups; existing transport retries still apply.

Recovery is limited to leaves whose parent is the current Edge device. It does
not discover missing intermediate gateways from scope IDs or bypass the
authorization chain required to query other devices in a nested hierarchy.
Periodic bulk refreshes can still remove identities omitted from their results.
