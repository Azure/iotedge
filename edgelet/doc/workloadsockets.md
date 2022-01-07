# Workload Sockets

This page only applies to 1.1.x releases from 1.1.5 onwards and 1.2.x releases from 1.2.4 onwards.

## Separate workload sockets

IoT Edge versions 1.1.5 and 1.2.4 introduced a mechanism to prevent modules from interfering with other users of the workload socket. Prior to these versions, the workload socket was always a single socket, `/run/iotedge/workload.sock` by default. Since all modules shared a single workload socket, a malicious module could deny service to other modules by modifying the permissions on the workload socket or sending continuous requests.

Versions 1.1.5 and 1.2.4 separated the workload socket so that each module uses its own workload socket. These sockets are placed in `/var/lib/aziot/edged/mnt`. For example, the edgeAgent and edgeHub modules now use independent sockets, `/var/lib/aziot/edged/mnt/edgeAgent.sock` and `/var/lib/aziot/edged/mnt/edgeHub.sock`, respectively. This separation prevents modules from interfering with the workload requests of other modules.

To maintain compatibility with previous versions, `/run/iotedge/workload.sock` remains as the legacy workload socket. Modules that were installed before an upgrade to 1.1.5/1.2.4 will continue to use the legacy workload socket, while modules installed after the upgrade will use separate workload sockets. A previously-installed module must be deleted and reinstalled to change from using the legacy workload socket to the new separate workload sockets.

## Socket throttling

In addition to separating the workload sockets, each workload socket now has a limit of 10 simultaneous requests per workload socket. As each module uses its own workload socket, each module may make at most 10 simultaneous requests. This prevents modules from denying service to others by making continuous requests.

If requests on a module socket exceed this limit, IoT Edge will deny all further requests on that module socket until the number of in-progress requests falls below 10. When IoT Edge denies a request, it closes the socket connection without sending a reply; this will be reported as an OS-level error (not an HTTP API error) to the calling module. It is always the caller's responsibility to retry a request that failed due to throttling.

In addition to closing the socket connection, IoT Edge will also log a message. The exact message may change between versions, but it will always specify that the module was throttled.

```
[ERR] Maximum concurrency reached, 10 simultaneous connections, dropping the connection request
```
