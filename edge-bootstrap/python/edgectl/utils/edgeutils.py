""" Module defines a class that implements several common utility functions """
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
    """ EdgeUtils defines a several common utility functions """
    @staticmethod
    def _remove_readonly_callback(func, path, excinfo):
        del func, excinfo
        os.chmod(path, stat.S_IWRITE)
        os.unlink(path)

    @staticmethod
    def delete_dir(dir_path):
        """
        Recursively deletes the directory identified by dir_path.

        Arguments:
            dir_path {str} -- directory path to delete

        Raises:
            Any exceptions returned by calling shutil.rmtree
        """
        try:
            if os.path.exists(dir_path):
                shutil.rmtree(dir_path, onerror=EdgeUtils._remove_readonly_callback)
        except OSError as ex:
            log.error('Error deleting directory: %s. Errno: %s, Error: %s',
                      dir_path, str(ex.errno), ex.strerror)
            raise

    @staticmethod
    def mkdir_if_needed(dir_path):
        """
        Recursively creates a directory hierarchy identified by dir_path.
        Results in a no-op if the directory already exists.

        Arguments:
            dir_path {str} -- directory path to create

        Raises:
            Any exceptions returned by calling os.makedirs except errno.EEXIST
        """
        try:
            os.makedirs(dir_path)
        except OSError as ex:
            if ex.errno != errno.EEXIST:
                log.error('Error creating directory: %s. Errno: %s, Error: %s',
                          dir_path, str(ex.errno), ex.strerror)
                raise

    @staticmethod
    def check_if_file_exists(file_path):
        """
        Checks if the file exists at file_path.

        Arguments:
            file_path {str} -- Path to the file.

        Returns:
            True -- If the file exists
            False -- IF the file does not exist or is not a file or file_path is None.
        """
        if file_path is None \
            or os.path.exists(file_path) is False \
            or os.path.isfile(file_path) is False:
            return False
        return True

    @staticmethod
    def check_if_directory_exists(dir_path):
        """
        Checks if the directory exists at dir_path.

        Arguments:
            file_path {str} -- Path to the directory.

        Returns:
            True -- If the file exists
            False -- IF the dir does not exist or is not a dir or dir_path is None.
        """
        if dir_path is None \
            or os.path.exists(dir_path) is False \
            or os.path.isdir(dir_path) is False:
            return False
        return True

    @staticmethod
    def copy_files(src_path, dst_path):
        """
        Copy file(s) or directory identified by src_path to dst_path.

        Arguments:
            src_path {str} -- Source file or directory.
            dst_path {str} -- Destination file or directory.

        Raises:
            OSError in the event a copy operation was unsuccessful.
        """
        try:
            shutil.copy2(src_path, dst_path)
        except OSError as ex:
            log.error('Error copying files: %s to %s. Errno: %s, Error: %s',
                      src_path, dst_path, str(ex.errno), ex.strerror)
            raise

    @staticmethod
    def get_hostname():
        """
        Obtains the FQDN hostname of the host.

        Returns:
            str -- Hostname string

        Raises:
            IOError in the event the hostname lookup operation failed.
        """
        try:
            return socket.getfqdn()
        except IOError as ex:
            log.error('Error determining hostname. Errno: %s, Error: %s',
                      str(ex.errno), ex.strerror)
            raise

    @staticmethod
    def sanitize_registry_data(address, username, password):
        """
        Helper function to convert docker registry data into a string while
        removing any senstive data such as credentials. This is primarily used
        for diagnostics purposes.

        Arguments:
            address {str} -- Registry address
            username {str} -- Registry username
            password {str} -- Registry password

        Returns:
            str -- Registry data string
        """
        del password
        return 'Address: {0}, Username: {1}, Password:******'.format(address, username)

    @staticmethod
    def prompt_password(password_description, min_len, max_len):
        """
        Helper method to present a CLI prompt to enter a password with the given length restrictions
        When entering the password, the characters are not displayed.
        CTRL-C exits the program.

        Arguments:
            password_description {str} -- Description of the password
            min_len {int} -- Minimum length of the password
            max_len {int} -- Maximum length of the password

        Returns:
            str -- User supplied password
        """
        done = False
        print('Press CTRL-C at anytime to exit.')
        while not done:
            try:
                msg_initial = 'Please enter the {0} private key passphrase. ' \
                              'Length should be >= {1} and <= {2}: '.format(password_description,
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
                    raise ValueError('Invalid response password length: ' + str(resp_len))
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                print(' ', str(ex_val))
                print('  Please try again.\n')

        return response_initial

    @staticmethod
    def sanitize_connection_string(connection_string):
        """
        Returns a connection string with the SharedAccessKey data stripped out.

        Arguments:
            connection_string {str} -- Connection string (IoTHub, Device, Module)

        Returns:
            str -- Connection string without any SharedAccessKey data
        """
        try:
            items = [(s[0], s[1] if s[0].lower() != 'sharedaccesskey' else '******') \
                    for s in [p.split('=', 2) for p in connection_string.split(';')]]
            return ';'.join(["%s=%s" % p for p in items])
        except (ValueError, IndexError):
            return '******'
