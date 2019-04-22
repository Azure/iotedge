#!/bin/bash
# usage: ./agent_final.sh <user> <hostname> <hostkey> [<hostname> <hostkey>]...

set -euo pipefail

# Update system-wide known_hosts file so agent can connect to runners

user="$1"
hosts=( )
host_key_pair=( )
id_rsa="$(eval echo ~$user)/.ssh/id_rsa"
suffix="$(grep -Po '^search \K.*' /etc/resolv.conf)"

touch /etc/ssh/ssh_known_hosts

for val in "${@:2}"; do
    host_key_pair=( "${host_key_pair[@]}" "$val" )
    if [ ${#host_key_pair[@]} -eq 2 ]; then

        set -- "${host_key_pair[@]}"
        host_key_pair=( )
        hosts=( "${hosts[@]}" "$1" )
        ipaddr="$(getent hosts "$1" | awk '{ print $1 }')"

        # Remove pre-existing entries for this host
        ssh-keygen -R "$1" -f /etc/ssh/ssh_known_hosts
        ssh-keygen -R "$1.$suffix" -f /etc/ssh/ssh_known_hosts

        # Append host key to known_hosts
        cat <<-EOF >> /etc/ssh/ssh_known_hosts
$1,$ipaddr $2
$1.$suffix $2
EOF

    fi
done

# Test that we really can:
# (1) SSH into the runners, and
# (2) make HTTP/S requests through the proxy

agent_name=$(hostname)

for host in "${hosts[@]}"; do
    # Linux or Windows
    ssh -i "$id_rsa" "$user@$host" uname && os='linux' || os='windows'
    if [ "$os" == 'linux' ]; then
        echo "Testing Linux runner '$host'"

        # Verify runner can use the proxy
        ssh -i "$id_rsa" "$user@$host" curl -x "http://$agent_name:3128" -L 'http://www.microsoft.com'
        ssh -i "$id_rsa" "$user@$host" curl -x "http://$agent_name:3128" -L 'https://www.microsoft.com'

        # Verify runner can't skirt the proxy (should time out after 5s)
        ssh -i "$id_rsa" "$user@$host" timeout 5 curl -L 'http://www.microsoft.com' && exit 1 || :
        ssh -i "$id_rsa" "$user@$host" timeout 5 curl -L 'https://www.microsoft.com' && exit 1 || :

        echo "Linux runner verified."
    else  # windows
        echo "Testing Windows runner '$host'"

        # Verify runner can use the proxy (should succeed)
        # **SSH terminal doesn't like the progress bar that Invoke-WebRequest
        #   tries to display, so use $ProgressPreference='SilentlyContinue' to
        #   supress it.
        ssh -i "$id_rsa" "$user@$host" "\$ProgressPreference='SilentlyContinue'; Invoke-WebRequest -UseBasicParsing -Proxy 'http://$agent_name:3128' 'http://www.microsoft.com'"
        ssh -i "$id_rsa" "$user@$host" "\$ProgressPreference='SilentlyContinue'; Invoke-WebRequest -UseBasicParsing -Proxy 'http://$agent_name:3128' 'https://www.microsoft.com'"

        # Verify runner can't skirt the proxy (should time out after 5s)
        ssh -i "$id_rsa" "$user@$host" "Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 'http://www.microsoft.com'" && exit 1 || :
        ssh -i "$id_rsa" "$user@$host" "Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 'https://www.microsoft.com'" && exit 1 || :

        echo "Windows runner verified."
    fi
done
