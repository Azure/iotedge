#!/bin/sh
set -e

# first arg is `-f` or `--some-option`
if [ "${1#-}" != "$1" ]; then
	set -- haproxy "$@"
fi

if [ "$1" = 'haproxy' ]; then
	shift # "haproxy"
	# if the user wants "haproxy", let's add a couple useful flags
	#   -W  -- "master-worker mode" (similar to the old "haproxy-systemd-wrapper"; allows for reload via "SIGUSR2")
	#   -db -- disables background mode
	set -- haproxy -W -db "$@"
fi

CERTS_DIR=/certs
EXPIRATION=$(date +"%Y-%m-%dT%H:%M:%SZ" -d@"$(( `date +%s`+90*24*60*60))")

mkdir -p ${CERTS_DIR}
exec /usr/bin/edgehub-proxy cert-server --common-name "${IOTEDGE_GATEWAYHOSTNAME}" --expiration "${EXPIRATION}" --combined "${CERTS_DIR}/iotedge.pem"  -- "$@"
