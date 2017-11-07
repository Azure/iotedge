import logging as log
import os, errno
import socket
import shutil

class EdgeUtils:

    @staticmethod
    def delete_dir(dir_path):
        try:
            if os.path.exists(dir_path):
                shutil.rmtree(dir_path)
        except OSError as ex:
            log.error('Error when deleting home directory: ' \
                      + dir_path + '. Errno ' \
                      + str(ex.errno) + ', Error:' + ex.strerror)
            raise

    @staticmethod
    def mkdir_if_needed(dir_path):
        try:
            os.mkdir(dir_path)
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
