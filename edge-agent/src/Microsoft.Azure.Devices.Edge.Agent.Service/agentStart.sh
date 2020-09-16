#!/bin/sh

###############################################################################
# Set up EdgeAgent to run as a non-root user at runtime, if allowed.
# 
# If this script is started as root:
#  1. It reads the EDGEAGENTUSER_ID environment variable, default UID=1000.
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
# is UID 1000.
#
# A user is created because at this time DotNet Core 2.x and 3.x can only install
# trust bundles into system stores or user stores.  We choose a user store in
# the code, so a writeable user directory is required.
###############################################################################
echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Agent"

TARGET_UID="${EDGEAGENTUSER_ID:-1000}"
cuid=$(id -u)

if [ $cuid -eq 0 ]
then
  # which user creation tool to use.
  usercreate=useradd
  if cat /etc/os-release | grep -i alpine > /dev/null
  then
    # Alpine only has "adduser"
    usercreate=adduser
  fi

  # Create the agent user id if it does not exist
  if ! getent passwd "${TARGET_UID}" >/dev/null
  then
    echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Creating UID ${TARGET_UID} as agent${TARGET_UID}"
    if [ $usercreate = useradd ]
    then
      useradd -ms /bin/bash -u "${TARGET_UID}" "agent${TARGET_UID}"
    else
      adduser -Ds /bin/sh -u "${TARGET_UID}" "agent${TARGET_UID}"
    fi
  fi

  username=$(getent passwd "${TARGET_UID}" | awk -F ':' '{ print $1; }')

  # If "StorageFolder" env variable exists, use as basepath, else use /tmp
  # same for backuppath
  agentstorage=$(env | grep -m 1 -i StorageFolder | awk -F '=' '{ print $2;}')
  agentbackup=$(env | grep -m 1 -i BackupFolder | awk -F '=' '{ print $2;}')
  storagepath=${agentstorage:-/tmp}/edgeAgent
  backuppath=${agentbackup:-/tmp}/edgeAgent_backup
  # If basepath/edgeAgent exists, make sure all files are owned by TARGET_UID
  if [ -d $storagepath ]
  then
    storageuid=$(stat -c "%u" "$storagepath")
    if [ ${TARGET_UID} -ne ${storageuid} ]
    then 
      echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Changing ownership of storage folder: ${storagepath}"
      chown -fR "${TARGET_UID}" "${storagepath}"
    fi
  fi
  # same for backuppath
  if [ -d $backuppath ]
  then
    backupuid=$(stat -c "%u" "$backuppath")
    if [ ${TARGET_UID} -ne ${backupuid} ]
    then 
      echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Changing ownership of backup folder: ${backuppath}"
      chown -fR "${TARGET_UID}" "${backuppath}"
    fi
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
  touch backup.json
  chown -f "${TARGET_UID}" backup.json
  chmod 600 backup.json

  exec su "$username" -c "/usr/bin/dotnet Microsoft.Azure.Devices.Edge.Agent.Service.dll"
else
  exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Agent.Service.dll
fi
