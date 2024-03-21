#!/bin/bash

set -eu
EXIT_STATUS=0

"$@" || EXIT_STATUS=$?

if [ $EXIT_STATUS -eq 153 ] ; then
    snapctl stop $SNAP_INSTANCE_NAME.aziot-edged
fi

exit $EXIT_STATUS
