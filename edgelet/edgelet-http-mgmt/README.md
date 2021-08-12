# Management APIs

The management APIs are available on the management socket, which by default is `/run/iotedge/mgmt.sock`. They deal with modules and their identities. Some of the APIs are exclusive to the `edgeAgent` module and not available to other callers.

The management APIs are divided into the following groups:
- [Device Management](doc/device_management.md)
- [Identity Management](doc/identity_management.md)
- [Module Management](doc/module_management.md)
- [System Information](doc/system_information.md)
