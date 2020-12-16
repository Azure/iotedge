#!/bin/bash

[ -n "$1" ] || exit 1
(($1 >= 1)) || exit 1

num_keypairs=$1

for i in `seq $num_keypairs`
do
    ssh-keygen -t rsa -b 4096 -N '' -f "id_rsa$i" || exit 1
done

comma=','
echo '{"keyinfo": [' > $AZ_SCRIPTS_OUTPUT_PATH
for i in `seq $num_keypairs`
do
    if ((i == $num_keypairs)); then comma=''; fi
    json="{\"privateKey\":\"$(cat id_rsa$i)\",\"publicKey\":\"$(cat id_rsa$i.pub)\"}$comma"
    echo "$json" >> $AZ_SCRIPTS_OUTPUT_PATH
done
echo ']}' >> $AZ_SCRIPTS_OUTPUT_PATH
