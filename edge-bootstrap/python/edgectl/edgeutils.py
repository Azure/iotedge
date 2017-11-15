from __future__ import print_function
import errno
import getpass
import logging as log
import os
import shutil
import socket
import stat
import sys


class EdgeUtils:

    @staticmethod
    def _remove_readonly(func, path, excinfo):
        os.chmod(path, stat.S_IWRITE)
        os.unlink(path)

    @staticmethod
    def delete_dir(dir_path):
        try:
            if os.path.exists(dir_path):
                shutil.rmtree(dir_path, onerror=EdgeUtils._remove_readonly)
        except OSError as ex:
            log.error('Error when deleting home directory: ' \
                      + dir_path + '. Errno ' \
                      + str(ex.errno) + ', Error:' + ex.strerror)
            raise

    @staticmethod
    def mkdir_if_needed(dir_path):
        try:
            os.makedirs(dir_path)
        except OSError as ex:
            if ex.errno != errno.EEXIST:
                log.error('Error when making home directory:' \
                        + dir_path \
                        + ' Errno ' + str(ex.errno) \
                        + ', Error:' + ex.strerror)
                raise

    @staticmethod
    def get_hostname():
        hostname = None
        try:
            hostname = socket.getfqdn()
            return hostname
        except IOError as ex:
            log.error('Error when determining hostname.' \
                        + 'Errno ' + str(ex.errno) \
                        + ', Error:' + ex.strerror)
            raise

    @staticmethod
    def sanitize_registry_data(address, username, password):
        return 'Address: {0}, Username: {1}, Password:******'.format(address, username)

    @staticmethod
    def prompt_password(password_type, min_len, max_len):
        done = False
        response = None
        print('Press CTRL-C at anytime to exit.')
        while not done:
            try:
                msg_initial = 'Please enter the {0} private key passphrase. ' \
                              'Length should be >= {1} and <= {2}: '.format(password_type,
                                                                            min_len,
                                                                            max_len)
                response_initial = getpass.getpass(msg_initial)
                resp_len = len(response_initial)
                if min_len <= resp_len <= max_len:
                    msg_verify = 'Please re-enter the passphrase: '
                    response_verify = getpass.getpass(msg_verify)
                    if response_initial != response_verify:
                        raise ValueError('Passwords did not match.')
                    else:
                        done = True
                else:
                    raise ValueError('Invalid Response Password Length: ' + str(resp_len))
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                print(' ', str(ex_val))
                print('  Please try again.\n')

        return response_initial
