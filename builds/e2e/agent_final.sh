#!/bin/bash
# usage: ./agent_final.sh <user> <hostname> <hostkey> [<hostname> <hostkey>]...

set -euo pipefail

# Update ~/.ssh/known_hosts so agent can connect to runners

user="$1"
hosts=( )
host_key_pair=( )
home="$(eval echo ~$user)"
suffix="$(grep -Po '^search \K.*' /etc/resolv.conf)"

mkdir -p "$home/.ssh"
chown "$user:$user" "$home/.ssh"
touch "$home/.ssh/known_hosts"

for val in "${@:2}"; do
    host_key_pair=( "${host_key_pair[@]}" "$val" )
    if [ ${#host_key_pair[@]} -eq 2 ]; then

        set -- "${host_key_pair[@]}"
        host_key_pair=( )
        hosts=( "${hosts[@]}" "$1" )
        ipaddr="$(getent hosts "$1" | awk '{ print $1 }')"

        # Remove pre-existing entries for this host
        ssh-keygen -R "$1" -f "$home/.ssh/known_hosts"
        ssh-keygen -R "$1.$suffix" -f "$home/.ssh/known_hosts"

        # Append host key to known_hosts
        cat <<-EOF >> "$home/.ssh/known_hosts"
$1,$ipaddr $2
$1.$suffix $2
EOF

    fi
done

chown "$user:$user" "$home/.ssh/known_hosts"

# Test that we really can:
# (1) SSH into the runners, and
# (2) make HTTP/S requests through the proxy

agent_name=$(hostname)

for host in "${hosts[@]}"; do
    # Linux or Windows
    ssh "$user@$host" uname
    if [ $? -eq 0 ]; then  # Linux
        echo "Testing Linux runner '$host'"

        # Verify runner can use the proxy
        ssh "$user@$host" curl -x "http://$agent_name:3128" -L 'http://www.microsoft.com'
        ssh "$user@$host" curl -x "http://$agent_name:3128" -L 'https://www.microsoft.com'

        # Verify runner can't skirt the proxy (should time out after 5s)
        ssh "$user@$host" timeout 5 curl -L 'http://www.microsoft.com' && exit 1 || :
        ssh "$user@$host" timeout 5 curl -L 'https://www.microsoft.com' && exit 1 || :

        echo "Linux runner verified."
    else  # Windows
        echo "Testing Windows runner '$host'"

        # Verify runner can use the proxy (should succeed)
        # When Invoke-WebRequest is invoked over SSH and -Proxy argument is added, we get an error back ("Access is
        # denied" 0x5 occurred while reading the console output buffer). Avoid this by wrapping the command in try.
        # Using 'ssh -t' also avoids the problem, but unfortunately swallows the return value.
        ssh "$user@$host" "try { Invoke-WebRequest -UseBasicParsing -Proxy 'http://$agent_name:3128' 'http://www.microsoft.com' } catch {}"
        ssh "$user@$host" "try { Invoke-WebRequest -UseBasicParsing -Proxy 'http://$agent_name:3128' 'https://www.microsoft.com' } catch {}"

        # Verify runner can't skirt the proxy (should time out after 5s)
        ssh "$user@$host" "Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 'http://www.microsoft.com'" && exit 1 || :
        ssh "$user@$host" "Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 'https://www.microsoft.com'" && exit 1 || :

        echo "Windows runner verified."
    fi
done
