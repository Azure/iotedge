import json
import logging as log
import os
from shutil import copy2
import edgectl.errors
import edgectl.edgeconstants as EC
from edgectl.certutil import generate_self_signed_certs_if_needed
from edgectl.certutil import get_ca_cert_file_path
from edgectl.certutil import get_server_cert_file_path
from edgectl.default  import EdgeDefault
from edgectl.edgeutils import EdgeUtils

class EdgeHostPlatform(object):

    @staticmethod
    def get_host_config_file_path():
        result = None
        edge_config_file_path = EdgeDefault.get_host_config_file_path()
        if edge_config_file_path and os.path.exists(edge_config_file_path):
            result = edge_config_file_path
        return result

    @staticmethod
    def get_home_dir():
        result = None
        edge_config_file_path = EdgeDefault.get_host_config_file_path()
        if edge_config_file_path:
            with open(edge_config_file_path, 'r') as input_file:
                data = json.load(input_file)
                result = data[EC.HOMEDIR_KEY]
        return result

    @staticmethod
    def get_certs_dir():
        result = None
        home_dir = EdgeHostPlatform.get_home_dir()
        if home_dir:
            certs_dir = os.path.join(home_dir, 'certs')
            if os.path.exists(certs_dir):
                result = certs_dir
        return result

    @staticmethod
    def get_ca_cert_file():
        result = None
        certs_dir = EdgeHostPlatform.get_certs_dir()
        if certs_dir:
            prefix = 'edge-device-ca'
            certs_dir = os.path.join(certs_dir,
                                     prefix,
                                     'cert')
            cert_file = os.path.join(certs_dir, prefix + '.cert.pem')
            if os.path.exists(cert_file):
                result = {
                    'dir': certs_dir,
                    'file_name': prefix + '.cert.pem',
                    'file_path': cert_file,
                }
        return result

    @staticmethod
    def get_ca_chain_cert_file():
        result = None
        certs_dir = EdgeHostPlatform.get_certs_dir()
        if certs_dir:
            prefix = 'edge-chain-ca'
            certs_dir = os.path.join(certs_dir,
                                     prefix,
                                     'cert')
            cert_file = os.path.join(certs_dir, prefix + '.cert.pem')
            if os.path.exists(cert_file):
                result = {
                    'dir': certs_dir,
                    'file_name': prefix + '.cert.pem',
                    'file_path': cert_file,
                }
        return result

    @staticmethod
    def get_hub_cert_file():
        result = None
        certs_dir = EdgeHostPlatform.get_certs_dir()
        if certs_dir:
            prefix = 'edge-hub-server'
            hub_certs_dir = os.path.join(certs_dir, prefix)
            server_cert_dir = os.path.join(hub_certs_dir, 'cert')
            cert_file = os.path.join(server_cert_dir, prefix + '.cert.pem')
            server_key_dir = os.path.join(hub_certs_dir, 'private')
            key_file = os.path.join(server_key_dir, prefix + '.key.pem')
            if os.path.exists(cert_file) and os.path.exists(key_file):
                result = {
                    'hub_cert_dir': hub_certs_dir,
                    'server_cert_file_name': prefix + '.cert.pem',
                    'server_key_file_name': prefix + '.key.pem',
                }
        return result

    @staticmethod
    def get_hub_cert_pfx_file():
        result = None
        certs_dir = EdgeHostPlatform.get_certs_dir()
        if certs_dir:
            prefix = 'edge-hub-server'
            certs_dir = os.path.join(certs_dir,
                                     prefix,
                                     'cert')
            cert_file = os.path.join(certs_dir, prefix + '.cert.pfx')
            if os.path.exists(cert_file):
                result = {
                    'dir': certs_dir,
                    'file_name': prefix + '.cert.pfx',
                    'file_path': cert_file
                }
        return result

    @staticmethod
    def install_edge_by_config_file(ip_config_file_path, edge_home_dir, host_name):
        if EdgeDefault.is_platform_supported():
            try:
                EdgeHostPlatform.__get_or_create_edge_config_dir()
                edge_config_file_path = EdgeDefault.get_host_config_file_path()
                copy2(ip_config_file_path, edge_config_file_path)
                EdgeHostPlatform.__setup_home_dir(edge_home_dir, host_name)
            except IOError as ex:
                log.error('Error copying user config file: %s.' \
                          ' Errno: %s, Error: %s', ip_config_file_path,
                          str(ex.errno), ex.strerror)
                raise edgectl.errors.EdgeFileAccessError('Cannot copy file',
                                                         ip_config_file_path)

    @staticmethod
    def install_edge_by_json_data(data, edge_home_dir, host_name):
        if EdgeDefault.is_platform_supported():
            try:
                EdgeHostPlatform.__get_or_create_edge_config_dir()
                edge_config_file_path = EdgeDefault.get_host_config_file_path()
                fd = os.open(edge_config_file_path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
                with os.fdopen(fd, 'w') as output_file:
                    output_file.write(data)
                EdgeHostPlatform.__setup_home_dir(edge_home_dir, host_name)
            except IOError as ex:
                log.error('Error writing to config file: %s.' \
                          ' Errno: %s, Error: %s', edge_config_file_path,
                          str(ex.errno), ex.strerror)
                msg = 'Cannot write configuration data to file'
                raise edgectl.errors.EdgeFileAccessError(msg,
                                                         edge_config_file_path)

    @staticmethod
    def uninstall_edge(home_dir):
        if EdgeDefault.is_platform_supported():
            EdgeHostPlatform.__delete_edge_config_dir()
            EdgeHostPlatform.__delete_home_dir(home_dir)

    @staticmethod
    def __setup_home_dir(home_dir, host_name):
        home_dir_path = os.path.realpath(home_dir)
        if os.path.exists(home_dir_path) is False:
            log.debug('Edge home dir not setup, creating dir: %s', home_dir_path)
        EdgeUtils.mkdir_if_needed(home_dir_path)
        certs_dir = os.path.join(home_dir_path, 'certs')
        EdgeUtils.mkdir_if_needed(certs_dir)
        modules_path = os.path.join(home_dir_path, 'modules')
        EdgeUtils.mkdir_if_needed(modules_path)
        edge_agent_dir = os.path.join(modules_path,
                                        EdgeDefault.get_agent_dir_name())
        EdgeUtils.mkdir_if_needed(edge_agent_dir)
        generate_self_signed_certs_if_needed(certs_dir, host_name)

    @staticmethod
    def __delete_home_dir(home_dir):
        home_dir_path = os.path.realpath(home_dir)
        if os.path.exists(home_dir_path):
            log.debug('Deleting Home Dir: %s', home_dir_path)
            path = os.path.join(home_dir_path, 'certs')
            EdgeUtils.delete_dir(path)
            path = os.path.join(home_dir_path, 'modules')
            EdgeUtils.delete_dir(path)

    @staticmethod
    def __get_or_create_edge_config_dir():
        result = None
        edge_config_dir = EdgeDefault.get_host_config_dir()
        log.debug('Found config directory: %s', edge_config_dir)
        if os.path.exists(edge_config_dir):
            result = edge_config_dir
        else:
            try:
                log.info('Config directory does not exist.' \
                         'Creating directory: %s', edge_config_dir)
                os.mkdir(edge_config_dir)
                result = edge_config_dir
            except OSError as ex:
                log.error('Error creating config directory: %s' \
                          ' Errno: %s, Error: %s', edge_config_dir,
                          str(ex.errno), ex.strerror)
                raise edgectl.errors.EdgeFileAccessError('Cannot create dir',
                                                         edge_config_dir)
        return result

    @staticmethod
    def __delete_edge_config_dir():
        edge_config_dir = EdgeDefault.get_host_config_dir()
        log.debug('Deleting Edge Config Dir:' + edge_config_dir)
        EdgeUtils.delete_dir(edge_config_dir)
