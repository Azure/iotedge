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
can still remove identities omitted from its response. Parent insertion is
conditional: it cannot replace a cached parent or undo a parent update or removal
that occurs while the module lookup is in flight.

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

This direct-leaf fallback makes no gateway lookup when the leaf lookup fails or returns no
identity, the leaf is disabled or has no parent scopes, the gateway is already
cached, or the leaf already has an authentication chain. After successful
recovery, other leaves reuse the cached gateway. There is no background retry
loop: a later successful leaf refresh can retry failed recovery after the shared
cooldown, or recover a gateway that was subsequently removed from the cache.
The existing lookup behavior for unknown or out-of-scope leaves is unchanged.
These counts refer to logical lookups; existing transport retries still apply.

This fallback is limited to leaves whose parent is the current Edge device. It does
not discover missing intermediate gateways from scope IDs or bypass the
authorization chain required to query other devices in a nested hierarchy.
Periodic bulk refreshes can still remove identities omitted from their results.

## Missing nested gateways in the device scope cache

When nested authentication lacks a usable chain, Edge Hub uses the supplied chain
as device-ID hints and repairs missing ancestors from the nearest usable cached
gateway downward, bootstrapping the local Edge identity if necessary. Each
gateway is retrieved on behalf of its validated parent, using the
existing `getDeviceAndModuleOnBehalfOf` endpoint with no module ID. The HTTP
point-lookup handler also attempts this recovery when the originating gateway's
chain is missing. No HTTP API or environment variable is added.

Gateway IDs, enabled status, Edge capability, scope relationships, and the
resulting cached chains are checked before proceeding. Malformed, cyclic, or
over-depth hints are rejected. Scope IDs alone are not used to invent missing
device IDs or trusted relationships.

Ancestor recovery fetches only missing identities. Restoring a gateway reconnects its cached
descendants without refreshing them, including targets still in their normal
refresh cooldown. When a missing module's response can supply its owner, no
separate owner lookup is made; an omitted Edge owner can instead be looked up
using its explicit device ID. If the target's chain is still unusable after
ancestor repair, its normal identity refresh remains subject to the existing
cooldown. Cached gateways, including disabled ones, are not
replaced by recovery, and delayed recovery responses cannot undo newer updates
or removals.

Concurrent ancestor recovery shares a lookup per gateway, with a cooldown per
gateway/parent pair after success, empty results, rejection, or failure.
Cooldown tracking is bounded to 1000 pairs; new pairs are deferred until an
entry expires when that limit is reached. The direct-leaf fallback shares this
mechanism for the local gateway. Ordinary target-identity refreshes retain their
existing retry behavior.

A nested leaf that eventually proves out of scope can still require ancestor
lookups first. Successful ancestors are then reused, rather than fetched for
each rejected leaf request. These are logical lookup counts; existing HTTP
forwarding and transport retries still apply.

There is no background recovery loop. Network failures preserve cached
identities; a later request can retry after the cooldown. Valid cached chains
remain usable offline, but identities absent from all reachable caches cannot
be recovered until an upstream source is available. Bulk refresh eviction and
discovery of ancestors absent from both chain hints and the cache are unchanged.
