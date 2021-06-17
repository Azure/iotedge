#!/bin/sh

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
  # Create the agent user id if it does not exist
  if ! getent passwd "${TARGET_UID}" >/dev/null
  then
    echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Creating UID ${TARGET_UID} as agent${TARGET_UID}"
    # Use "useradd" if it is available.
    if command -v useradd >/dev/null
    then
      useradd -ms /bin/bash -u "${TARGET_UID}" "agent${TARGET_UID}"
    else
      adduser -Ds /bin/sh -u "${TARGET_UID}" "agent${TARGET_UID}"
    fi
  fi
  
  username=$(getent passwd "${TARGET_UID}" | awk -F ':' '{ print $1; }')

  # If "StorageFolder" env variable exists, use as basepath, else use /tmp
  # same for BackupFolder
  hubstorage=$(env | grep -m 1 -i StorageFolder | awk -F '=' '{ print $2; }')
  hubbackup=$(env | grep -m 1 -i BackupFolder | awk -F '=' '{ print $2; }')
  storagepath=${hubstorage:-/tmp}/edgehub
  backuppath=${hubbackup:-/tmp}/edgehub_backup
  # If basepath/edgehub exists, make sure all files are owned by TARGET_UID
  if [ -d $storagepath ]
  then
    echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Changing ownership of storage folder: ${storagepath} to ${TARGET_UID}"
    chown -fR "${TARGET_UID}" "${storagepath}"
   fi
  # same for BackupFolder
  if [ -d $backuppath ]
  then
    echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Changing ownership of backup folder: ${backuppath} to ${TARGET_UID}"
    chown -fR "${TARGET_UID}" "${backuppath}"
  fi

  exec su "$username" -c "/usr/local/bin/watchdog"
else
  exec /usr/local/bin/watchdog
fi
