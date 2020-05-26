# In-memory database backup and restore of IoT Edge system modules

By default, the IoT Edge system modules Edge Agent and Edge Hub store data in an on device disk-based database store. This database store is used by these system modules to store configuration data and Edge Hub specifically uses this database to store messages.

Support for an in-memory database store is also provided for customers who wish to limit the amount of disk I/O on the device and can be enabled by setting the `UsePersistentStorage` environment variable of the module to `false`.

**An important point to note about the in-memory database store is that by default anything written to this store is not persisted to disk at any point and therefore can be lost whenever the module stops running.**

To remediate this problem, the system modules also support a backup and restore of the in-memory message store on graceful shutdown and subsequent startup so that the data in the in-memory store is only backed-up to persistent store (disk) on module shutdown. On the next startup, the in-memory database will first be restored from the backup created previously.

## __Use case__

Customers who wish to reduce the amount of disk I/O performed on the device by IoT Edge system modules and also wish to avoid the loss of unprocessed messages/data will find this feature particularly useful.

## __Configuration__

To enable backup and restore of the in-memory database stores, please set the `EnableNonPersistentStorageBackup` environment variable of the system module to `true`. There are some other configuration changes that need to be made to enable this feature successfully:

### __Required configuration__

* The environment variable `UsePersistentStorage` has to be set to `false` for this feature to take any effect. If this flag is not set to `false`, the system module will simply use a disk-based database store by default thus eliminating the need for backup and restore.

### __Optional configuration__

* A directory from the host can be mounted into the system module as the directory where backups will be stored by the system module.
* An environment variable `BackupFolder` will have to be specified for the system module so as to make it aware of the path to the backup folder mounted in the previous step.

In case the value of the `BackupFolder` is not specified (and mounted), the backups will be created on the container's file system.
With the config changes specified above, the system module will start backing up data in the database store to the specified `BackupFolder` which is mounted on persistent storage (disk) on graceful shutdown and will attempt to restore from it on the next startup.

During regular execution, the modules will continue to store messages in-memory (as they do right now) and no disk I/O will be performed.

If the `BackupFolder` value is not specified and the `EnableNonPersistentStorageBackup` environment variable is set to `true`, the system module will store backups in:

* The explicitly defined storage folder for the module. The storage folder is mounted into the system module and an environment variable `StorageFolder` is specified with a value equal to the path of the mounted folder within the module.
* the default storage folder (under /tmp within the module) if no `StorageFolder` is explicitly specified.

In case a restore operation fails due to corrupt data in the backup, the system module will ignore all the backup data and start from a clean state where there are no pending messages or data. **This implies that old data that wasn't processed might be lost**.

The existing backup will be deleted after a restore attempt irrespective of whether the restore was successful or not.

***Please note that it is possible that the system module is killed before it has a chance to create a backup which might lead to lost data. To prevent this from happening, customers should specify a larger value of [`StopTimeout`][1] (in the container create options) for the system module to give it appropriate time to create a backup before the IoT edge daemon stops it. The current recommended value is 60 seconds at the least.***

[1]: https://docs.docker.com/engine/api/v1.30/#operation/ContainerCreate