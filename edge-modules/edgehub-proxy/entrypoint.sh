#!/bin/sh

set -e

CERTS_DIR=/certs

if [ "${IOTEDGE_WORKLOADURI:0:4}" == "unix" ]
then
    UDS="--unix-socket ${IOTEDGE_WORKLOADURI:7}"
    HOSTNAME="http://localhost/"
else
    UDS=""
    HOSTNAME=${IOTEDGE_WORKLOADURI}
fi

EXPIRATION=$(date +"%Y-%m-%dT%H:%M:%SZ" -d@"$(( `date +%s`+90*24*60*60))")
BODY="{\"commonName\":\"${IOTEDGE_GATEWAYHOSTNAME}\",\"expiration\":\"${EXPIRATION}\"}"
URL="${HOSTNAME}modules/${IOTEDGE_MODULEID}/genid/${IOTEDGE_MODULEGENERATIONID}/certificate/server?api-version=2018-06-28"

echo "Getting server certificate with expiration '${EXPIRATION}' from ${URL}"

RESPONSE=$(curl -s ${UDS} -X POST --data "${BODY}" "${URL}")

echo "Recieved certificate."

echo "Saving certificate to ${CERTS_DIR}/iotedge.pem"
mkdir -p ${CERTS_DIR}
echo ${RESPONSE} | jq -r '.certificate' > ${CERTS_DIR}/iotedge.pem
echo ${RESPONSE} | jq -r '.privateKey.bytes' >> ${CERTS_DIR}/iotedge.pem

echo "Starting HAProxy..."
exec /docker-entrypoint.sh "$@"
