#!/bin/bash
# usage: ./known_hosts.sh <user> <hostname> <hostkey> [<hostname> <hostkey>]...

set -euo pipefail

user="$1"
host_key_pair=( )
home="$(eval echo ~$user)"
suffix="$(grep -Po '^search \K.*' /etc/resolv.conf)"

mkdir -p "$home/.ssh"
chown "$user:$user" "$home/.ssh"

for val in "${@:2}"; do

host_key_pair=( "${host_key_pair[@]}" "$val" )
if [ ${#host_key_pair[@]} -eq 2 ]; then

set -- "${host_key_pair[@]}"
host_key_pair=( )

ipaddr="$(getent hosts $1 | awk '{ print $1 }')"

cat <<-EOF >> "$home/.ssh/known_hosts"
$1, $ipaddr, $2
$1.$suffix, $2
EOF

fi
done

chown "$user:$user" "$home/.ssh/known_hosts"
