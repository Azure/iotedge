This file documents how to set up the VSTS job that runs our E2E proxy tests.

The overall setup is to have two VMs:

- The "agent VM" has full network connectivity and runs the SSH tasks defined in the E2E proxy VSTS job. It also runs an HTTP proxy (squid).

- The "runner VM" runs the proxy tests themselves. It has no network connectivity except to talk to the agent VM, thus all its interactions with Azure IoT Hub need to occur through the squid proxy on the agent VM.

Follow the steps below to deploy the two VMs and set them up. The Azure CLI `az` is required.


```sh
cd ./builds/e2e/

# ----------
# Parameters


# Name of Azure subscription
subscription_name='<>'

# Location of the resource group
location='<>'

# Name of the resource group
resource_group_name='<>'

# Name of the Linux user for the VMs
vms_linux_username='vsts'

# Name of the VNet and subnet that the VSTS agent VM is in.
# The new VMs have to be in the same VNet as the VSTS agent VM.
vms_vnet_name='<>'
vms_vnet_subnet_name='default'

# Names of the agent and runner VMs. Used to resolve them via DNS for the tests.
vsts_agent_vm_name='e2eproxyvstsagent'
vsts_runner1_vm_name='e2eproxyvstsrunner1'

# Name of the dynamically-provisioned public IP for the agent VM
vsts_agent_vm_public_ip_name='e2eproxy'


# -------
# Execute


# Log in to Azure subscription
az login
az account set -s "$subscription_name"


# Create SSH key for the VMs
keyfile="$(realpath ./id_rsa)"
ssh-keygen -t rsa -b 4096 -N '' -f "$keyfile"


# Create an SSH service connection in VSTS using $vsts_agent_vm_name and $keyfile


# Deploy the VMs
az group deployment create --resource-group "$resource_group_name" --name 'e2e-proxy' --template-file ./proxy-deployment-template.json --parameters "$(
    jq -n \
        --arg vsts_agent_vm_name "$vsts_agent_vm_name" \
        --arg vsts_runner1_vm_name "$vsts_runner1_vm_name" \
        --arg vms_linux_ssh_public_key "$(cat $keyfile.pub)" \
        --arg vms_linux_username "$vms_linux_username" \
        --arg vms_vnet_name "$vms_vnet_name" \
        --arg vms_vnet_subnet_name "$vms_vnet_subnet_name" \
        --arg vsts_agent_vm_public_ip_name "$vsts_agent_vm_public_ip_name" \
        '{
            "vms_linux_ssh_public_key": { "value": $vms_linux_ssh_public_key },
            "vms_linux_username": { "value": $vms_linux_username },
            "vms_vnet_name": { "value": $vms_vnet_name },
            "vms_vnet_subnet_name": { "value": $vms_vnet_subnet_name },
            "vsts_agent_vm_name": { "value": $vsts_agent_vm_name },
            "vsts_agent_vm_public_ip_name": { "value": $vsts_agent_vm_public_ip_name },
            "vsts_runner1_vm_name": { "value": $vsts_runner1_vm_name }
        }'
)"

# Get the public IP of the agent VM
vsts_agent_public_ip="$(az network public-ip show --resource-group "$resource_group_name" --name "$vsts_agent_vm_public_ip_name" --query 'ipAddress' --output tsv)"


# Get the address prefix of the subnet
subnet_address_prefix="$(az network vnet show --resource-group "$resource_group_name" --name "$vms_vnet_name" | jq --arg 'vms_vnet_subnet_name' "$vms_vnet_subnet_name" '.subnets | map(select(.name == $vms_vnet_subnet_name))[0] | .addressPrefix' -r)"


# Copy SSH private key to agent VM so that it can ssh to runner VMs
scp "$keyfile" "$vms_linux_username@$vsts_agent_public_ip:/home/$vms_linux_username/.ssh/id_rsa"


# Install pre-reqs on agent VM
> ./setup.sh cat <<-EOF
set -euo pipefail

sudo apt-get install -y jq squid

> ~/squid.conf cat <<-INNEREOF
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
INNEREOF

sudo mv /etc/squid/squid.conf /etc/squid/squid.conf.orig
sudo mv ~/squid.conf /etc/squid/squid.conf
sudo chown root:root /etc/squid/squid.conf
sudo chmod 0644 /etc/squid/squid.conf

sudo systemctl stop squid & sudo systemctl kill squid
sudo systemctl start squid
EOF
scp -i "$keyfile" ./setup.sh "$vms_linux_username@$vsts_agent_public_ip:/home/$vms_linux_username/"
rm ./setup.sh
ssh -i "$keyfile" "$vms_linux_username@$vsts_agent_public_ip" bash "/home/$vms_linux_username/setup.sh"
ssh -i "$keyfile" "$vms_linux_username@$vsts_agent_public_ip" rm "/home/$vms_linux_username/setup.sh"


# Verify proxy works (should succeed)
ssh -ti "$keyfile" "$vms_linux_username@$vsts_agent_public_ip" ssh -i "/home/$vms_linux_username/.ssh/id_rsa" "$vms_linux_username@$vsts_runner1_vm_name" curl -x "http://$vsts_agent_vm_name:3128" -L 'http://www.microsoft.com'
ssh -ti "$keyfile" "$vms_linux_username@$vsts_agent_public_ip" ssh -i "/home/$vms_linux_username/.ssh/id_rsa" "$vms_linux_username@$vsts_runner1_vm_name" curl -x "http://$vsts_agent_vm_name:3128" -L 'https://www.microsoft.com'

# Verify proxy is required (should time out after 5s)
ssh -ti "$keyfile" "$vms_linux_username@$vsts_agent_public_ip" ssh -i "/home/$vms_linux_username/.ssh/id_rsa" "$vms_linux_username@$vsts_runner1_vm_name" timeout 5 curl -L 'http://www.microsoft.com'
ssh -ti "$keyfile" "$vms_linux_username@$vsts_agent_public_ip" ssh -i "/home/$vms_linux_username/.ssh/id_rsa" "$vms_linux_username@$vsts_runner1_vm_name" timeout 5 curl -L 'https://www.microsoft.com'


# Install test pre-reqs on runner VM
> ./runner-prereqs.sh cat <<-EOF
set -euo pipefail

curl -x 'http://$vsts_agent_vm_name:3128' 'https://packages.microsoft.com/config/ubuntu/18.04/prod.list' > ./microsoft-prod.list
sudo mv ./microsoft-prod.list /etc/apt/sources.list.d/

curl -x 'http://$vsts_agent_vm_name:3128' 'https://packages.microsoft.com/keys/microsoft.asc' | gpg --dearmor > microsoft.gpg
sudo mv ./microsoft.gpg /etc/apt/trusted.gpg.d/

sudo 'http_proxy=http://$vsts_agent_vm_name:3128' 'https_proxy=http://$vsts_agent_vm_name:3128' apt-get update
sudo 'http_proxy=http://$vsts_agent_vm_name:3128' 'https_proxy=http://$vsts_agent_vm_name:3128' apt-get install -y moby-cli moby-engine

> ~/proxy-env.override.conf cat <<-INNEREOF
[Service]
Environment="http_proxy=http://$vsts_agent_vm_name:3128"
Environment="https_proxy=http://$vsts_agent_vm_name:3128"
INNEREOF
sudo mkdir -p /etc/systemd/system/docker.service.d/
sudo cp ~/proxy-env.override.conf /etc/systemd/system/docker.service.d/

sudo systemctl daemon-reload
sudo systemctl restart docker
EOF
scp -i "$keyfile" -o "ProxyCommand ssh $vms_linux_username@$vsts_agent_public_ip nc %h %p" ./runner-prereqs.sh "$vms_linux_username@$vsts_runner1_vm_name:/home/$vms_linux_username/"
rm ./runner-prereqs.sh
ssh -ti "$keyfile" "$vms_linux_username@$vsts_agent_public_ip" ssh -i "/home/$vms_linux_username/.ssh/id_rsa" "$vms_linux_username@$vsts_runner1_vm_name" bash "/home/$vms_linux_username/runner-prereqs.sh"
ssh -ti "$keyfile" "$vms_linux_username@$vsts_agent_public_ip" ssh -i "/home/$vms_linux_username/.ssh/id_rsa" "$vms_linux_username@$vsts_runner1_vm_name" rm "/home/$vms_linux_username/runner-prereqs.sh"
```
