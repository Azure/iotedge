#!/bin/bash

# Installs the pre-reqs (currently only libsnappy) on the machine.

echo Install Libsnappy
sudo apt-get update
sudo apt-get install -y libsnappy1v5

exit 0
