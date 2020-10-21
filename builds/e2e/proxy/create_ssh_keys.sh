#!/bin/bash

[ -n "$1" ] || exit 1
(($1 >= 1)) || exit 1

num_keypairs=$1

for i in `seq $num_keypairs`
do
    ssh-keygen -t rsa -b 4096 -N '' -f "id_rsa$i" || exit 1
done

echo '{"keyinfo": [' > $AZ_SCRIPTS_OUTPUT_PATH
for i in `seq $(($num_keypairs-1))`
do
    json="{\"privateKey\":\"$(cat id_rsa$i)\",\"publicKey\":\"$(cat id_rsa$i.pub)\"},"
    echo $json >> $AZ_SCRIPTS_OUTPUT_PATH
done
# the last JSON object cannot have a trailing comma
json="{\"privateKey\":\"$(cat id_rsa$num_keypairs)\",\"publicKey\":\"$(cat id_rsa$num_keypairs.pub)\"}"
echo "$json" >> $AZ_SCRIPTS_OUTPUT_PATH
echo ']}' >> $AZ_SCRIPTS_OUTPUT_PATH
