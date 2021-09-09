#!/bin/sh

###############################################################################
# Set up EdgeAgent to run as a non-root user at runtime, if allowed.
# 
# If this script is started as root:
#  1. It reads the EDGEAGENTUSER_ID environment variable, default UID=13622.
#  2. If the User ID does not exist as a user, create it.
#  3. If "StorageFolder" env variable exists, use as basepath, else use /tmp
#     Do same for backuppath
#  4. If basepath/edgeAgent exists, make sure all files are owned by EDGEAGENTUSER_ID
#     Do same for backuppath/edgeAgent_backup
#  5. Make sure file specified by IOTEDGE_MANAGEMENTURI is owned by EDGEAGENTUSER_ID
#  6. Make sure /app/backup.json is writeable.
#  7. Set user id as EDGEAGENTUSER_ID.
# then start Edge Agent.
#
# This preserves backwards compatibility with earlier versions of edgeAgent and
# allows some flexibility in the assignment of the edgeagent user id. The default 
# is UID 13622.
#
# A user is created because at this time DotNet Core 2.x and 3.x can only install
# trust bundles into system stores or user stores.  We choose a user store in
# the code, so a writeable user directory is required.
###############################################################################
echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Agent"

TARGET_UID="${EDGEAGENTUSER_ID:-13622}"
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
  agentstorage=$(env | grep -m 1 -i StorageFolder | awk -F '=' '{ print $2; }')
  agentbackup=$(env | grep -m 1 -i BackupFolder | awk -F '=' '{ print $2; }')
  storagepath=${agentstorage:-/tmp}/edgeAgent
  backuppath=${agentbackup:-/tmp}/edgeAgent_backup
  # If basepath/edgeAgent exists, make sure all files are owned by TARGET_UID
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

  # Make sure file specified by IOTEDGE_MANAGEMENTURI is owned by TARGET_UID
  # Strip "unix://" prefix, and if that is a file that exists, change the ownership.
  mgmt=${IOTEDGE_MANAGEMENTURI#unix:\/\/}
  if [ -e "$mgmt" ]
  then
    echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Changing ownership of management socket: ${mgmt}"
    chown -f "${TARGET_UID}" "$mgmt"
  fi

  # Ensure backup.json is writeable. 
  # Need only to check if user has set this to a non-default location,
  # because default location is in storageFolder, which was fixed above.
  backupjson=$(env | grep -m 1 -i BackupConfigFilePath | awk -F '=' '{ print $2;}')
  if [ -e "$backupjson" ]
  then 
    echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Changing ownership of configuration back : ${backupjson}"
    chown -f "${TARGET_UID}" "$backupjson"
    chmod 600 "$backupjson"
  fi

  exec su "$username" -c "/usr/bin/dotnet Microsoft.Azure.Devices.Edge.Agent.Service.dll"
else
  exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Agent.Service.dll
fi
