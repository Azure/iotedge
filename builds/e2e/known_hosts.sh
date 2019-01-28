#!/bin/bash

set -euo pipefail

user="$1"
host="$2"
key="$3"

suffix="$(grep -Po '^search \K.*' /etc/resolv.conf)"
ipaddr="$(getent hosts $host | awk '{ print $1 }')"
home="$(eval echo ~$user)"

mkdir -p "$home/.ssh"
chown "$user:$user" "$home/.ssh"

> "$home/.ssh/known_hosts" cat <<-EOF
$host, $ipaddr, $key
$host.$suffix, $key
EOF

chown "$user:$user" "$home/.ssh/known_hosts"
