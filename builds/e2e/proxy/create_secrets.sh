#!/bin/bash

[ "$#" -eq 2 ] || exit 1
echo $1 | grep -E -q '^[0-9]+$' || exit 1
echo $2 | grep -E -q '^[0-9]+$' || exit 1

num_keypairs=$1
num_passwords=$2

for i in `seq $num_keypairs`
do
    ssh-keygen -t rsa -b 4096 -N '' -f "id_rsa$i" || exit 1
done

passwords=( )
for i in `seq $num_passwords`
do
    passwords=( "${passwords[@]}" "$(openssl rand -base64 32)" )
done

comma=','
echo '{"keyinfo": [' > $AZ_SCRIPTS_OUTPUT_PATH
for i in `seq $num_keypairs`
do
    if ((i == $num_keypairs)); then comma=''; fi
    json="{\"privateKey\":\"$(cat id_rsa$i)\",\"publicKey\":\"$(cat id_rsa$i.pub)\"}$comma"
    echo "$json" >> $AZ_SCRIPTS_OUTPUT_PATH
done
comma=','
echo '], "passwords": [' >> $AZ_SCRIPTS_OUTPUT_PATH
for i in `seq $num_passwords`
do
    if ((i == $num_passwords)); then comma=''; fi
    json="\"${passwords[$i-1]}\"$comma"
    echo "$json" >> $AZ_SCRIPTS_OUTPUT_PATH
done
echo ']}' >> $AZ_SCRIPTS_OUTPUT_PATH
