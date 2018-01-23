from __future__ import print_function
import errno
import getpass
import logging as log
import os
import shutil
import socket
import stat
import sys


class EdgeUtils(object):

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
            log.error('Error deleting directory: ' \
                      + dir_path + '. Errno ' \
                      + str(ex.errno) + ', Error:' + ex.strerror)
            raise

    @staticmethod
    def mkdir_if_needed(dir_path):
        try:
            os.makedirs(dir_path)
        except OSError as ex:
            if ex.errno != errno.EEXIST:
                log.error('Error creating directory:' \
                        + dir_path \
                        + ' Errno ' + str(ex.errno) \
                        + ', Error:' + ex.strerror)
                raise
    @staticmethod
    def check_if_file_exists(file_path):
        if file_path is None \
            or os.path.exists(file_path) is False \
            or os.path.isfile(file_path) is False:
            return False
        return True

    @staticmethod
    def check_if_directory_exists(dir_path):
        if dir_path is None \
            or os.path.exists(dir_path) is False \
            or os.path.isdir(dir_path) is False:
            return False
        return True

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

    @staticmethod
    def sanitize_connection_string(conn_str):
        """ Returns a connection string with the SharedAccessKey data stripped out."""
        try:
            items = [(s[0], s[1] if s[0].lower() != 'sharedaccesskey' else '******') \
                    for s in [p.split('=', 2) for p in conn_str.split(';')]]
            return ';'.join(["%s=%s" % p for p in items])
        except:
            return '******'
