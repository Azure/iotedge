#!/bin/sh

VERNUM=$(cat module.json | grep -Eo '\"version\": \"([0-9.]+)\"' | grep -Eo '[0-9.]+')
echo \#\!/bin/sh > get_vernum.sh
echo echo ${VERNUM} >> get_vernum.sh
chmod +x get_vernum.sh
