#!/bin/sh
$SNAP/usr/bin/setpriv --clear-groups --reuid snap_aziotedge --regid snap_aziotedge -- "$@"
