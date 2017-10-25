"""This module provides the main functionality of Azure Edge Runtime Control.

Invocation flow:
  1. Read, validate and process the input (args, `stdin`).
  2. Execute user commands and control the Edge
  3. Write status, errors logs to `stdout`
  4. Exit.
"""
from __future__ import print_function
import logging as log
import sys
from default import EdgeDefault
from edgecli import EdgeCLI

def coremain():
    """
    The main function.
    Pre-process args and run the main program.
    Return exit status code.
    """
    if EdgeDefault.is_platform_supported():
        cli = EdgeCLI('edgectl')
        try:
            cli.process_cli_args()
            cli.execute_command()
        except Exception as ex:
            print('Error "{0}" occurred. Arguments {1}.'.format(ex.message, ex.args))
            sys.exit(1)
    else:
        log.critical('Unsupported Platform. Exiting.')
        sys.exit(1)

    return

if __name__ == '__main__':
    coremain()
