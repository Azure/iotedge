#!/bin/sh

if [[ "$TARGETARCH" == 'arm' ]] || [[ "$TARGETARCH" == 'arm64' ]]; then
  export OptimizeForPerformance=false
  export MqttEventsProcessorThreadCount=1
fi

###############################################################################
# Set up EdgeHub to run as a non-root user at runtime, if allowed.
#
# If this script is started as root:
#  1. It reads the EDGEHUBUSER_ID environment variable, default UID=13623.
#  2. If the User ID does not exist as a user, create it.
#  3. If "StorageFolder" env variable exists, use as basepath, else use /tmp
#     Do same for backuppath
#  4. If basepath/edgehub exists, make sure all files are owned by EDGEHUBUSER_ID
#     Do same for backuppath/edgehub_backup
#  6. Set user id as EDGEHUBUSER_ID.
# then start Edge Hub.
#
# This preserves backwards compatibility with earlier versions of edgeHub and
# allows some flexibility in the assignment of the edgehub user id. The default
# is UID 13623.
#
# A user is created because at this time DotNet Core 2.x and 3.x can only install
# trust bundles into system stores or user stores.  We choose a user store in
# the code, so a writeable user directory is required.
###############################################################################
echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Hub"

TARGET_UID="${EDGEHUBUSER_ID:-13623}"
cuid=$(id -u)

if [ $cuid -eq 0 ]
then
  # Create the hub user id if it does not exist
  if ! getent passwd "${TARGET_UID}" >/dev/null
  then
    echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Creating UID ${TARGET_UID} as edgehubuser"
    adduser -D -S -s /bin/sh -u "${TARGET_UID}" edgehubuser
  fi

  username=$(getent passwd "${TARGET_UID}" | awk -F ':' '{ print $1; }')

  # If "StorageFolder" env variable exists, use as basepath, else use /tmp
  # same for BackupFolder
  hubstorage=$(env | grep -m 1 -i StorageFolder | awk -F '=' '{ print $2; }')
  hubbackup=$(env | grep -m 1 -i BackupFolder | awk -F '=' '{ print $2; }')
  storagepath=${hubstorage:-/tmp}/edgeHub
  backuppath=${hubbackup:-/tmp}/edgeHub_backup

  # If basepath/edgeHub exists, make sure all files are owned by TARGET_UID
  # Otherwise, create the path and set the correct permissions
  if [ -d $storagepath ]
  then
    echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Changing ownership of storage folder: ${storagepath} to ${TARGET_UID}"
    chown -fR "${TARGET_UID}" "${storagepath}"
  else
    echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Creating storage folder: ${storagepath}"
    mkdir -p -m 700 "${storagepath}"
    chown -R "${TARGET_UID}" "${storagepath}"
  fi
  # same for BackupFolder
  if [ -d $backuppath ]
  then
    echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Changing ownership of backup folder: ${backuppath} to ${TARGET_UID}"
    chown -fR "${TARGET_UID}" "${backuppath}"
  else
    echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Creating backup folder: ${backuppath}"
    mkdir -p -m 700 "${backuppath}"
    chown -R "${TARGET_UID}" "${backuppath}"
  fi

  exec su "$username" -c "/usr/bin/dotnet Microsoft.Azure.Devices.Edge.Hub.Service.dll"
else
  exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Hub.Service.dll
fi
