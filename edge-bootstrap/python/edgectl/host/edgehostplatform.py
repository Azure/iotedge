from __future__ import print_function
import json
import logging as log
import os
import platform
import sys
from edgectl.config import EdgeConfigDirInputSource
from edgectl.config import EdgeConstants as EC
from edgectl.config import EdgeDefault
from edgectl.host import EdgeDockerClient
from edgectl.utils import EdgeCertUtil
from edgectl.utils import EdgeUtils
import edgectl.errors


class EdgeHostPlatform(object):
    _min_passphrase_len = 4
    _max_passphrase_len = 1023

    @staticmethod
    def get_docker_uri():
        dc = EdgeDockerClient()
        engine_os = dc.get_os_type()
        return EdgeDefault.get_docker_uri(platform.system(), engine_os)

    @staticmethod
    def is_deployment_supported(deployment):
        return EdgeDefault.is_deployment_supported(platform.system(), deployment)

    @staticmethod
    def get_supported_docker_engines():
        return EdgeDefault.get_docker_container_types(platform.system())

    @staticmethod
    def get_home_dir():
        return os.path.realpath(EdgeDefault.get_home_dir(platform.system()))

    @staticmethod
    def _read_json_config_file(json_config_file):
        try:
            with open(json_config_file, 'r') as input_file:
                data = json.load(input_file)
                return data
        except IOError as ex_os:
            msg = 'Error reading config file: {0}. Errno: {1}, Error {2}'.format(json_config_file,
                                                                                 str(ex_os.errno),
                                                                                 ex_os.strerror)
            log.error(msg)
            raise edgectl.errors.EdgeFileAccessError(msg, json_config_file)
        except ValueError as ex_value:
            msg = 'Could not parse {0}. JSON Parser Error: {1}'.format(json_config_file,
                                                                       str(ex_value))
            log.error(msg)
            raise edgectl.errors.EdgeFileParseError(msg, json_config_file)


    @staticmethod
    def choose_platform_config_dir(user_input_path, user_input_option):
        """
        Utility function that chooses a Edge config directory in the
        precedence order of:
        1) Env variable EDGECONFIGDIR
        2) User input via user_input_path
        3) Default Path

        Args:
            user_input_path: (string) A user supplied config dir path. Can be None or ''.
            user_input_option: (enum EdgeConfigDirInputSource member):
                               Use NONE when user_input_path is None or empty

        Return:
            Tuple:
               [0]: Path to the Edge config dir.
               [1]: An EdgeConfigDirInputSource enum member indicating which dir was chosen
        """
        edge_config_dir = None
        choice = None

        env_config_dir = os.getenv(EC.ENV_EDGECONFIGDIR, None)
        if env_config_dir and env_config_dir.strip() != '':
            edge_config_dir = os.path.realpath(env_config_dir)
            log.info('Using environment variable %s as IoT Edge configuration dir: %s',
                     EC.ENV_EDGECONFIGDIR, edge_config_dir)
            choice = EdgeConfigDirInputSource.ENV
        elif user_input_path and user_input_path.strip() != '':
            edge_config_dir = os.path.realpath(user_input_path)
            log.info('Using user configured IoT Edge configuration dir: %s', edge_config_dir)
            choice = user_input_option
        else:
            edge_config_dir = os.path.realpath(EdgeDefault.get_config_dir(platform.system()))
            log.info('Using default IoT Edge configuration dir: %s', edge_config_dir)
            choice = EdgeConfigDirInputSource.DEFAULT

        return (edge_config_dir, choice)

    @staticmethod
    def _get_host_config_dir():
        edge_config_dir = None
        result = None
        host = platform.system()
        log.debug('Searching Edge config dir in env var %s', EC.ENV_EDGECONFIGDIR)
        env_config_dir = os.getenv(EC.ENV_EDGECONFIGDIR, None)
        if env_config_dir and env_config_dir.strip() != '':
            edge_config_dir = os.path.realpath(env_config_dir)
        else:
            meta_config_file_path = EdgeDefault.get_meta_conf_file_path()
            log.debug('Searching Edge config dir in config file %s', meta_config_file_path)
            if meta_config_file_path and os.path.exists(meta_config_file_path):
                data = EdgeHostPlatform._read_json_config_file(meta_config_file_path)
                config_dir = data[EC.CONFIG_DIR_KEY]
                if config_dir != '':
                    edge_config_dir = os.path.realpath(config_dir)
            else:
                edge_config_dir = os.path.realpath(EdgeDefault.get_config_dir(host))
                log.debug('Using default Edge config dir %s', edge_config_dir)

        if edge_config_dir and os.path.isdir(edge_config_dir):
            log.debug('Found config directory: %s', edge_config_dir)
            result = edge_config_dir

        return result

    @staticmethod
    def get_host_config_file_path():
        result = None
        edge_config_dir = EdgeHostPlatform._get_host_config_dir()

        if edge_config_dir:
            edge_config_file_path = os.path.join(edge_config_dir,
                                                 EdgeDefault.get_config_file_name())
            if os.path.exists(edge_config_file_path):
                result = edge_config_file_path
        return result

    @staticmethod
    def _get_edge_home_dir():
        result = None
        edge_config_file_path = EdgeHostPlatform.get_host_config_file_path()
        if edge_config_file_path:
            data = EdgeHostPlatform._read_json_config_file(edge_config_file_path)
            result = data[EC.HOMEDIR_KEY]
            result = os.path.realpath(result)
        return result

    @staticmethod
    def _get_certs_dir():
        result = None
        home_dir = EdgeHostPlatform._get_edge_home_dir()
        if home_dir:
            certs_dir = os.path.join(home_dir, 'certs')
            if os.path.exists(certs_dir):
                result = certs_dir
        return result

    @staticmethod
    def get_root_ca_cert_file():
        result = None
        certs_dir = EdgeHostPlatform._get_certs_dir()
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
        certs_dir = EdgeHostPlatform._get_certs_dir()
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
        certs_dir = EdgeHostPlatform._get_certs_dir()
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
        certs_dir = EdgeHostPlatform._get_certs_dir()
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
        edge_config_file_path = EdgeHostPlatform._setup_edge_config_dir(edge_config)
        config_data = None
        with open(ip_config_file_path, 'r') as input_file:
            config_data = input_file.read()
        EdgeHostPlatform._create_config_file(edge_config_file_path, config_data, 'Edge config file')
        EdgeHostPlatform._setup_home_dir(edge_config, True)

    @staticmethod
    def install_edge_by_json_data(edge_config, force_regen_certs_bool):
        json_data = edge_config.to_json()
        edge_config_file_path = EdgeHostPlatform._setup_edge_config_dir(edge_config)
        EdgeHostPlatform._create_config_file(edge_config_file_path, json_data, 'Edge config file')
        EdgeHostPlatform._setup_home_dir(edge_config, force_regen_certs_bool)

    @staticmethod
    def uninstall_edge(home_dir):
        EdgeHostPlatform._clear_edge_config_dir()
        EdgeHostPlatform._clear_home_dir(home_dir)

    @staticmethod
    def _create_dir(dir_path, dir_type):
        try:
            EdgeUtils.mkdir_if_needed(dir_path)
        except OSError as ex:
            msg = 'Error creating {0} directory {1}'.format(dir_type, dir_path)
            log.error(msg)
            raise edgectl.errors.EdgeFileAccessError(msg, dir_path)

    @staticmethod
    def _delete_dir(dir_path, dir_type):
        try:
            EdgeUtils.delete_dir(dir_path)
        except OSError as ex:
            msg = 'Error deleting {0} directory {1}'.format(dir_type, dir_path)
            log.error(msg)
            raise edgectl.errors.EdgeFileAccessError(msg, dir_path)

    @staticmethod
    def _create_config_file(file_path, data, file_type_diagnostic):
        try:
            log.debug('Creating Config File: %s', file_path)
            fd = os.open(file_path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
            with os.fdopen(fd, 'w') as output_file:
                output_file.write(data)
        except OSError as ex:
            msg = 'Error creating {0}: {1}. ' \
                  'Errno: {2}, Error: {3}'.format(file_type_diagnostic,
                                                  file_path, str(ex.errno), ex.strerror)
            log.error(msg)
            raise edgectl.errors.EdgeFileAccessError(msg, file_path)

    @staticmethod
    def _delete_config_file(file_path, file_type_diagnostic):
        try:
            if os.path.exists(file_path):
                os.unlink(file_path)
        except OSError as ex:
            msg = 'Error deleteing {0}: {1}. ' \
                  'Errno: {2}, Error: {3}'.format(file_type_diagnostic,
                                                  file_path, str(ex.errno), ex.strerror)
            log.error(msg)
            raise edgectl.errors.EdgeFileAccessError(msg, file_path)

    @staticmethod
    def _setup_home_dir(edge_config, force_regen_certs_bool):
        home_dir = edge_config.home_dir
        # setup edge directory structure
        home_dir_path = os.path.realpath(home_dir)
        if os.path.isdir(home_dir_path) is False:
            log.debug('Edge home dir not setup, creating dir: %s', home_dir_path)
            EdgeHostPlatform._create_dir(home_dir_path, 'Edge home')
        certs_dir = os.path.join(home_dir_path, 'certs')
        if os.path.isdir(certs_dir) is False:
            log.debug('Edge certs dir not setup, creating dir: %s', certs_dir)
            EdgeHostPlatform._create_dir(certs_dir, 'Edge certs')
        modules_path = os.path.join(home_dir_path, 'modules')
        if os.path.isdir(modules_path) is False:
            log.debug('Edge modules dir not setup, creating dir: %s', modules_path)
            EdgeHostPlatform._create_dir(modules_path, 'Edge modules')
        if edge_config.use_self_signed_certificates():
            EdgeHostPlatform._generate_self_signed_certs_if_needed(edge_config,
                                                                   certs_dir,
                                                                   force_regen_certs_bool)
        else:
            EdgeHostPlatform._generate_certs_using_device_ca_if_needed(edge_config,
                                                                       certs_dir,
                                                                       force_regen_certs_bool)
    @staticmethod
    def _clear_home_dir(home_dir):
        home_dir_path = os.path.realpath(home_dir)
        if os.path.exists(home_dir_path):
            log.debug('Clearing Home Dir: %s', home_dir_path)
            path = os.path.join(home_dir_path, 'certs')
            EdgeHostPlatform._delete_dir(path, 'certs')
            path = os.path.join(home_dir_path, 'modules')
            EdgeHostPlatform._delete_dir(path, 'modules')

    @staticmethod
    def _setup_meta_edge_config_dir(edge_config_dir):
        meta_config_dir = EdgeDefault.get_edge_ctl_config_dir()
        if os.path.isdir(meta_config_dir) is False:
            log.info('Meta config directory does not exist.' \
                     'Creating directory: %s', meta_config_dir)
            EdgeHostPlatform._create_dir(meta_config_dir, 'Edge meta config')
        if edge_config_dir is None:
            edge_config_dir = ''
        meta_config_dict = {EC.CONFIG_DIR_KEY: edge_config_dir}
        json_data = json.dumps(meta_config_dict, indent=2, sort_keys=True)
        meta_config_file_path = EdgeDefault.get_meta_conf_file_path()
        EdgeHostPlatform._create_config_file(meta_config_file_path,
                                             json_data,
                                             'Edge meta config file')

    @staticmethod
    def _clear_edge_meta_config_dir():
        meta_config_file_path = EdgeDefault.get_meta_conf_file_path()
        log.debug('Deleting meta Edge config file: %s', meta_config_file_path)
        EdgeHostPlatform._delete_config_file(meta_config_file_path, 'Edge meta config')

    @staticmethod
    def _setup_edge_config_dir(edge_config):
        edge_config_dir = edge_config.config_dir
        if edge_config.config_dir_source == EdgeConfigDirInputSource.USER_PROVIDED:
            EdgeHostPlatform._setup_meta_edge_config_dir(edge_config_dir)
        else:
            EdgeHostPlatform._clear_edge_meta_config_dir()

        if os.path.exists(edge_config_dir) is False:
            log.info('IoT Edge Config directory does not exist.' \
                     'Creating directory: %s', edge_config_dir)
            EdgeHostPlatform._create_dir(edge_config_dir, 'Edge config')

        edge_config_file_path = os.path.join(edge_config_dir,
                                             EdgeDefault.get_config_file_name())
        return edge_config_file_path

    @staticmethod
    def _clear_edge_config_dir():
        edge_config_file_path = EdgeHostPlatform.get_host_config_file_path()
        log.debug('Deleting Edge config file: %s', edge_config_file_path)
        EdgeHostPlatform._delete_config_file(edge_config_file_path, 'Edge config')
        EdgeHostPlatform._clear_edge_meta_config_dir()

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
                device_ca_phrase = EdgeHostPlatform._prompt_password('Edge Device',
                                                                     bypass_opts,
                                                                     'deviceCAPassphraseFilePath')

            agent_ca_phrase = edge_config.agent_ca_passphrase
            if agent_ca_phrase is None or agent_ca_phrase == '':
                bypass_opts = ['--agent-ca-passphrase', '--agent-ca-passphrase-file']
                agent_ca_phrase = EdgeHostPlatform._prompt_password('Edge Agent',
                                                                    bypass_opts,
                                                                    'agentCAPassphraseFilePath')

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
                agent_ca_phrase = EdgeHostPlatform._prompt_password('Edge Agent',
                                                                    bypass_opts,
                                                                    'agentCAPassphraseFilePath')

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
    def _prompt_password(cert_type, bypass_options, config_file_setting):
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

        config_file_setting = '"security.certificates.<option>.' + config_file_setting + '"'
        print('\n',
              '\n********************************************************************************'
              '\nPlease enter a passphrase for the', cert_type, 'private key.',
              '\n\nTo prevent this prompt from appearing, input the required passphrase',
              '\nor generate the private key without a passphrase.',
              '\n\n When using the command line options to setup the IoT Edge runtime:',
              '\n - Enter the passphrase via the command line options:',
              '\n  ', options_str,
              '\n - When opting not to use a passphrase, use command line option:',
              '\n   --auto-cert-gen-force-no-passwords.',
              '\n\n When using the --config-file to setup the runtime:',
              '\n - Set the input passphrase file via JSON configuration item:',
              '\n  ', config_file_setting,
              '\n - When opting not to use a passphrase, set JSON configuration item:',
              '\n   "security.certificates.<option>.forceNoPasswords" to true.'
              '\n********************************************************************************')
        return EdgeUtils.prompt_password(cert_type,
                                         EdgeHostPlatform._min_passphrase_len,
                                         EdgeHostPlatform._max_passphrase_len)
