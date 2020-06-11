#!/bin/sh

# The most simple script i could come up with
# for simulating a blocking thing - could be
# `nginx -g 'daemon off;' or anything like that.
for i in `seq 1 1000`; do
  echo "$i\t `date +%s`"
  sleep 1
done
