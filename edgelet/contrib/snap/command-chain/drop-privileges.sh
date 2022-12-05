#!/bin/sh
setpriv --clear-groups --reuid snap_aziotedge --regid snap_aziotedge -- "$@"
