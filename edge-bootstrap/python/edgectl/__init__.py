"""This module provides the main functionality of Azure Edge Runtime Control.

Invocation flow:
  1. Read, validate and process the input (args, `stdin`).
  2. Execute user commands and control the Edge
  3. Write status, errors logs to `stdout`
  4. Exit.
"""

import sys
import pkg_resources
from edgectl.edgecli import EdgeCLI

PACKAGE_NAME = 'azure-iot-edge-runtime-ctl'
PROGRAM_NAME = 'iotedgectl'

def coremain():
    """
    The main function.
    Pre-process args and run the main program.
    Return exit status code.
    """
    version = pkg_resources.require(PACKAGE_NAME)[0].version
    cli = EdgeCLI(PROGRAM_NAME, version)
    return cli.execute_user_command()

if __name__ == '__main__':
    sys.exit(coremain())
