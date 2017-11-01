import logging as log
import os, errno
import socket

class EdgeUtils:
    @staticmethod
    def mkdir_if_needed(dir_path):
        try:
            os.mkdir(dir_path)
        except OSError as ex:
            if ex.errno != errno.EEXIST:
                log.error('Error Observed When Making Home Directory:' \
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
            log.error('Error Observed When Determining Hostname.' \
                        + 'Errno ' + str(ex.errno) \
                        + ', Error:' + ex.strerror)
            raise
