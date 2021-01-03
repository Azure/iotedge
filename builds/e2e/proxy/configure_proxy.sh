#!/bin/bash

set -euo pipefail

user="$1"
subnet_address_prefix="$2"

echo 'Installing squid'

for i in `seq 3`
do
    sleep 5
    apt-get update || continue
    apt-get install -y jq squid && break
done

echo 'Configuring squid'

> ~/squid.conf cat <<-EOF
acl localnet src $subnet_address_prefix

acl Safe_ports port 80
acl Safe_ports port 443
acl SSL_ports port 443

acl CONNECT method CONNECT  

# Deny requests to certain unsafe ports
http_access deny !Safe_ports

# Deny CONNECT to other than secure SSL ports
http_access deny CONNECT !SSL_ports

# Only allow cachemgr access from localhost
http_access allow localhost manager
http_access deny manager

# We strongly recommend the following be uncommented to protect innocent
# web applications running on the proxy server who think the only
# one who can access services on "localhost" is a local user
http_access deny to_localhost

# Allow access from your local networks.
http_access allow localnet
http_access allow localhost

# And finally deny all other access to this proxy
http_access deny all

http_port 3128

coredump_dir /var/spool/squid

refresh_pattern . 0 20% 4320
EOF

mv /etc/squid/squid.conf /etc/squid/squid.conf.orig
mv ~/squid.conf /etc/squid/squid.conf
chown root:root /etc/squid/squid.conf
chmod 0644 /etc/squid/squid.conf

systemctl stop squid
systemctl start squid
