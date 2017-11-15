from __future__ import print_function
import json
import logging as log
import os
from shutil import copy2
import sys
import edgectl.errors
import edgectl.edgeconstants as EC
from edgectl.certutil import EdgeCertUtil
from edgectl.default  import EdgeDefault
from edgectl.edgeutils import EdgeUtils

class EdgeHostPlatform(object):
    _min_passphrase_len = 4
    _max_passphrase_len = 1023

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
                result = os.path.realpath(result)
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
    def get_root_ca_cert_file():
        result = None
        certs_dir = EdgeHostPlatform.get_certs_dir()
        if certs_dir:
            prefix = 'edge-device-ca'
            certs_dir = os.path.join(certs_dir,
                                     prefix,
                                     'cert')

            cert_file = os.path.join(certs_dir, prefix + '-root.cert.pem')
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
    def install_edge_by_config_file(edge_config, ip_config_file_path):
        try:
            EdgeHostPlatform._get_or_create_edge_config_dir()
            edge_config_file_path = EdgeDefault.get_host_config_file_path()
            copy2(ip_config_file_path, edge_config_file_path)
            EdgeHostPlatform._setup_home_dir(edge_config, True)
        except IOError as ex:
            log.error('Error copying user config file: %s.' \
                      ' Errno: %s, Error: %s', ip_config_file_path,
                      str(ex.errno), ex.strerror)
            raise edgectl.errors.EdgeFileAccessError('Cannot copy file', ip_config_file_path)

    @staticmethod
    def install_edge_by_json_data(edge_config, force_regen_certs_bool):
        try:
            json_data = edge_config.to_json()
            EdgeHostPlatform._get_or_create_edge_config_dir()
            edge_config_file_path = EdgeDefault.get_host_config_file_path()
            fd = os.open(edge_config_file_path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
            with os.fdopen(fd, 'w') as output_file:
                output_file.write(json_data)
            EdgeHostPlatform._setup_home_dir(edge_config, force_regen_certs_bool)
        except IOError as ex:
            log.error('Error writing to config file: %s.' \
                      ' Errno: %s, Error: %s', edge_config_file_path,
                      str(ex.errno), ex.strerror)
            msg = 'Cannot write configuration data to file'
            raise edgectl.errors.EdgeFileAccessError(msg, edge_config_file_path)

    @staticmethod
    def uninstall_edge(home_dir):
        EdgeHostPlatform._delete_edge_config_dir()
        EdgeHostPlatform._delete_home_dir(home_dir)

    @staticmethod
    def _setup_home_dir(edge_config, force_regen_certs_bool):
        home_dir = edge_config.home_dir
        # setup edge directory structure
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

        if edge_config.use_self_signed_certificates():
            EdgeHostPlatform._generate_self_signed_certs_if_needed(edge_config,
                                                                   certs_dir,
                                                                   force_regen_certs_bool)
        else:
            EdgeHostPlatform._generate_certs_using_device_ca_if_needed(edge_config,
                                                                       certs_dir,
                                                                       force_regen_certs_bool)

    @staticmethod
    def _delete_home_dir(home_dir):
        home_dir_path = os.path.realpath(home_dir)
        if os.path.exists(home_dir_path):
            log.debug('Deleting Home Dir: %s', home_dir_path)
            path = os.path.join(home_dir_path, 'certs')
            EdgeUtils.delete_dir(path)
            path = os.path.join(home_dir_path, 'modules')
            EdgeUtils.delete_dir(path)

    @staticmethod
    def _get_or_create_edge_config_dir():
        result = None
        edge_config_dir = EdgeDefault.get_host_config_dir()
        log.debug('Found config directory: %s', edge_config_dir)
        if os.path.exists(edge_config_dir):
            result = edge_config_dir
        else:
            try:
                log.info('Config directory does not exist.' \
                         'Creating directory: %s', edge_config_dir)
                EdgeUtils.mkdir_if_needed(edge_config_dir)
                result = edge_config_dir
            except OSError as ex:
                log.error('Error creating config directory: %s' \
                          ' Errno: %s, Error: %s', edge_config_dir,
                          str(ex.errno), ex.strerror)
                raise edgectl.errors.EdgeFileAccessError('Cannot create dir',
                                                         edge_config_dir)
        return result

    @staticmethod
    def _delete_edge_config_dir():
        edge_config_dir = EdgeDefault.get_host_config_dir()
        log.debug('Deleting Edge Config Dir: %s', edge_config_dir)
        EdgeUtils.delete_dir(edge_config_dir)

    @staticmethod
    def _generate_self_signed_certs_if_needed(edge_config, certs_dir, force_regen_certs_bool):
        if force_regen_certs_bool or \
            EdgeHostPlatform._check_if_cert_regen_is_required(certs_dir):
            EdgeHostPlatform._generate_self_signed_certs(edge_config, certs_dir)

    @staticmethod
    def _generate_certs_using_device_ca_if_needed(edge_config, certs_dir, force_regen_certs_bool):
        if force_regen_certs_bool or \
            EdgeHostPlatform._check_if_cert_regen_is_required(certs_dir):
            EdgeHostPlatform._generate_certs_using_device_ca(edge_config, certs_dir)

    @staticmethod
    def _generate_certs_common(cert_util, edge_config, certs_dir, agent_ca_phrase):
        cert_util.export_cert_artifacts_to_dir('edge-device-ca', certs_dir)

        cert_util.create_intermediate_ca_cert('edge-agent-ca',
                                              365,
                                              'edge-device-ca',
                                              'Edge Agent CA',
                                              True,
                                              agent_ca_phrase)
        cert_util.export_cert_artifacts_to_dir('edge-agent-ca',
                                               certs_dir)

        cert_util.create_server_cert('edge-hub-server',
                                     365,
                                     'edge-agent-ca',
                                     edge_config.hostname)

        cert_util.export_cert_artifacts_to_dir('edge-hub-server',
                                               certs_dir)
        cert_util.export_server_pfx_cert('edge-hub-server', certs_dir)

        prefixes = ['edge-agent-ca', 'edge-device-ca']
        cert_util.chain_ca_certs('edge-chain-ca', prefixes, certs_dir)

    @staticmethod
    def _generate_self_signed_certs(edge_config, certs_dir):
        log.info('Generating self signed certificates at: %s', certs_dir)

        device_ca_phrase = None
        agent_ca_phrase = None
        if edge_config.force_no_passwords is False:
            device_ca_phrase = edge_config.device_ca_passphrase
            if device_ca_phrase is None or device_ca_phrase == '':
                bypass_opts = ['--device-ca-passphrase', '--device-ca-passphrase-file']
                device_ca_phrase = EdgeHostPlatform._prompt_password('Edge Device', bypass_opts)

            agent_ca_phrase = edge_config.agent_ca_passphrase
            if agent_ca_phrase is None or agent_ca_phrase == '':
                bypass_opts = ['--agent-ca-passphrase', '--agent-ca-passphrase-file']
                agent_ca_phrase = EdgeHostPlatform._prompt_password('Edge Agent', bypass_opts)

        cert_util = EdgeCertUtil()
        cert_util.create_root_ca_cert('edge-device-ca',
                                      365,
                                      edge_config.certificate_subject_dict,
                                      device_ca_phrase)
        EdgeHostPlatform._generate_certs_common(cert_util, edge_config, certs_dir, agent_ca_phrase)

    @staticmethod
    def _generate_certs_using_device_ca(edge_config, certs_dir):
        log.info('Generating Device CA based certificates at: %s', certs_dir)

        agent_ca_phrase = None
        if edge_config.force_no_passwords is False:
            agent_ca_phrase = edge_config.agent_ca_passphrase
            if agent_ca_phrase is None or agent_ca_phrase == '':
                bypass_opts = ['--agent-ca-passphrase', '--agent-ca-passphrase-file']
                agent_ca_phrase = EdgeHostPlatform._prompt_password('Edge Agent', bypass_opts)

        cert_util = EdgeCertUtil()
        cert_util.set_root_ca_cert('edge-device-ca',
                                   edge_config.device_ca_cert_file_path,
                                   edge_config.owner_ca_cert_file_path,
                                   edge_config.device_ca_chain_cert_file_path,
                                   edge_config.device_ca_private_key_file_path,
                                   edge_config.device_ca_passphrase)

        EdgeHostPlatform._generate_certs_common(cert_util, edge_config, certs_dir, agent_ca_phrase)

    @staticmethod
    def _check_if_cert_file_exists(dir_path, prefix, sub_dir, suffix):
        path = os.path.join(dir_path, prefix, sub_dir, prefix + suffix)
        result = os.path.exists(path)
        if result:
            log.debug('Cert file ok:' + path)
        else:
            log.debug('Cert file does not exist:' + path)
        return result

    @staticmethod
    def _check_if_cert_regen_is_required(certs_dir):
        result = True
        path = os.path.realpath(certs_dir)
        device_ca = EdgeHostPlatform._check_if_cert_file_exists(path,
                                                                'edge-device-ca',
                                                                'cert',
                                                                '-root.cert.pem')

        agent_ca = EdgeHostPlatform._check_if_cert_file_exists(path,
                                                               'edge-agent-ca',
                                                               'cert',
                                                               '.cert.pem')

        hub_server_pfx = EdgeHostPlatform._check_if_cert_file_exists(path,
                                                                     'edge-hub-server',
                                                                     'cert',
                                                                     '.cert.pfx')

        ca_chain = EdgeHostPlatform._check_if_cert_file_exists(path,
                                                               'edge-chain-ca',
                                                               'cert',
                                                               '.cert.pem')
        if device_ca and agent_ca and hub_server_pfx and ca_chain:
            result = False

        return result

    @staticmethod
    def _prompt_password(cert_type, bypass_options):
        options_str = ''
        options_len = len(bypass_options)
        index = 0
        for option in bypass_options:
            options_str += option
            if index < options_len - 1:
                options_str += ' or '
            else:
                options_str += '.'
            index += 1

        print('\n',
              '\n********************************************************************************'
              '\nYou are being prompted to enter a passphrase for the',
              cert_type, 'private key.',
              '\n\nTo prevent this prompt from appearing, enter the passphrase via the command',
              '\nline options', options_str,
              '\n - If you choose not to supply any passphrases, use command line option',
              '\n   --auto-cert-gen-force-no-passwords.',
              '\n - If using --config-file to setup the runtime, setup the input file',
              '\n   with the same options described above.'
              '\n********************************************************************************')
        return EdgeUtils.prompt_password(cert_type,
                                         EdgeHostPlatform._min_passphrase_len,
                                         EdgeHostPlatform._max_passphrase_len)
