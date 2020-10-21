#!/bin/bash

for i in `seq $1`
do
    ssh-keygen -t rsa -b 4096 -N '' -f "id_rsa$i" || exit 1
done

echo '{\"keyinfo\": [' > $AZ_SCRIPTS_OUTPUT_PATH
for i in `seq $1`
do
    json="{\"privateKey\":\"$(cat id_rsa$i)\",\"publicKey\":\"$(cat 'id_rsa$i.pub')\"},"
    echo $json >> $AZ_SCRIPTS_OUTPUT_PATH
done
echo ']}' >> $AZ_SCRIPTS_OUTPUT_PATH
