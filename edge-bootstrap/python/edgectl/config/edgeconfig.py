import json
import logging as log
import os
import platform
import re
import edgectl.errors
from edgectl.config.edgeconstants import EdgeConfigDirInputSource
from edgectl.config.edgeconstants import EdgeConstants as EC
from edgectl.config.default import EdgeDefault
from edgectl.utils import EdgeUtils


class EdgeDeploymentConfigDocker(object):
    def __init__(self):
        self._type = EC.DEPLOYMENT_DOCKER_KEY
        self._uri = ''
        self._uri_endpoint = ''
        self._uri_port = ''
        self._edge_image = ''
        self._edge_image_repository = ''
        self._edge_image_name = ''
        self._edge_image_tag = ''
        self._registries = []
        self._logging_driver = ''
        self._logging_options = {}

    @property
    def type(self):
        return self._type

    @property
    def uri(self):
        return self._uri

    @property
    def uri_endpoint(self):
        return self._uri_endpoint

    @property
    def uri_port(self):
        return self._uri_port

    @uri.setter
    def uri(self, value):
        is_valid = False
        if value is not None:
            length = len(value)
            if length > 0:
                pattern = re.compile(r'^\s*([a-z]+://)(\S+)$')
                match_obj = pattern.match(value)
                if match_obj:
                    self._uri_protocol = match_obj.group(1)
                    if self._uri_protocol in ['http://', 'https://', 'tcp://']:
                        pattern = re.compile(r'^\s*([a-z]+://)(\S+):([0-9]+)$')
                        match_obj = pattern.match(value)
                        if match_obj:
                            self._uri_endpoint = match_obj.group(2)
                            self._uri_port = match_obj.group(3)
                            self._uri = value
                            is_valid = True
                    if self._uri_protocol in ['npipe://']:
                        # Docker engine on Windows is at a well-known path.
                        self._uri_endpoint = '\\\\.\\pipe\\docker_engine'
                        self._uri_port = ''
                        self._uri = value
                        is_valid = True
                    elif self._uri_protocol in ['unix://']:
                        pattern = re.compile(r'^\s*([a-z]+://)(\S+)$')
                        match_obj = pattern.match(value)
                        if match_obj:
                            self._uri_endpoint = match_obj.group(2)
                            self._uri_port = ''
                            self._uri = value
                            is_valid = True
        if is_valid is False:
            raise ValueError('Invalid docker Uri: ' + str(value))

    @property
    def edge_image(self):
        return self._edge_image

    @edge_image.setter
    def edge_image(self, value):
        is_valid = False
        if value is not None:
            length = len(value)
            if length > 0:
                pattern = re.compile(r'^\s*([^\r\n\t\f /]+)/(\S+):([a-zA-Z0-9\.-]+)$')
                match_obj = pattern.match(value)
                if match_obj:
                    is_valid = True
                    self._edge_image = value
                    log.debug('Found Edge Agent image:' + self._edge_image)
                    self._edge_image_repository = match_obj.group(1)
                    log.debug('Found registry:' + self._edge_image_repository)
                    self._edge_image_name = match_obj.group(2)
                    log.debug('Found image name:' + self._edge_image_name)
                    self._edge_image_tag = match_obj.group(3)
                    log.debug('Found image tag:' + self._edge_image_tag)
        if is_valid is False:
            raise ValueError('Invalid Edge Agent image: ' + str(value))

    @property
    def edge_image_repository(self):
        return self._edge_image_repository

    @property
    def edge_image_name(self):
        return self._edge_image_name

    @property
    def edge_image_tag(self):
        return self._edge_image_tag

    @property
    def registries(self):
        return self._registries

    def registry_exists(self, server_address):
        for registry in self._registries:
            if registry['address'] == server_address:
                return registry
        return None

    def add_registry(self, server_address, username='', password=''):
        if server_address is not None:
            length = len(server_address)
            if length > 0:
                existing = self.registry_exists(server_address)
                if existing is not None:
                    self._registries.remove(existing)
                registry = {'address': server_address,
                            'username': username,
                            'password': password}
                self._registries.append(registry)

    @property
    def logging_driver(self):
        return self._logging_driver

    @logging_driver.setter
    def logging_driver(self, value):
        is_valid = False
        if value is not None:
            length = len(value)
            if length > 0:
                self._logging_driver = value
                is_valid = True

        if is_valid is False:
            raise ValueError('Invalid docker logging driver: ' + str(value))

    @property
    def logging_options(self):
        return self._logging_options

    def add_logging_option(self, option_key, option_value):
        is_valid = False
        if option_key is not None and option_value is not None:
            len_key = len(option_key)
            len_val = len(option_value)
            if len_key > 0 and len_val > 0:
                if option_key not in self._logging_options:
                    self._logging_options[option_key] = option_value
                    is_valid = True

        if is_valid is False:
            raise ValueError('Invalid docker logging option: '
                             + str(option_key) + ' ' + str(option_value))

    def __str__(self):
        result = 'Deployment Type:\t' + self.type + '\n'
        result += 'Docker Engine URI:\t' + self.uri + '\n'
        result += 'Edge Agent Image:\t' + self.edge_image + '\n'
        result += 'Registries:' + '\n'
        for registry in self.registries:
            result += '\t\t\t'
            reg_str = EdgeUtils.sanitize_registry_data(registry['address'],
                                                       registry['username'],
                                                       registry['password'])
            result += reg_str + '\n'
        result += 'Logging Driver:\t\t' + self.logging_driver + '\n'
        options = self.logging_options
        for key in options:
            result += '\t\t\t' + str(key) + ': ' + options[key] + '\n'
        return result

    def to_dict(self):
        d = {}
        d[EC.DOCKER_URI_KEY] = self.uri
        d[EC.EDGE_RUNTIME_IMAGE_KEY] = self.edge_image
        d[EC.REGISTRIES_KEY] = self.registries
        logs_dict = {}
        logs_dict[EC.DOCKER_LOGGING_DRIVER_KEY] = self.logging_driver
        logs_dict[EC.DOCKER_LOGGING_DRIVER_OPTS_KEY] = self.logging_options
        d[EC.DOCKER_LOGGING_OPTS_KEY] = logs_dict
        return d

class EdgeHostConfig(object):
    _supported_schemas = ['1']
    _default_schema_version = '1'
    _supported_log_levels = [EC.EDGE_RUNTIME_LOG_LEVEL_INFO,
                             EC.EDGE_RUNTIME_LOG_LEVEL_DEBUG]
    security_option_self_signed = EC.SELFSIGNED_KEY
    security_option_pre_installed = EC.PREINSTALL_KEY
    _supported_security_options = [security_option_self_signed,
                                   security_option_pre_installed]

    def __init__(self):
        self._schema_version = EdgeHostConfig._default_schema_version
        self._config_dir = ''
        self._config_dir_source = EdgeConfigDirInputSource.NONE
        self._home_dir = ''
        self._connection_string = ''
        self._hostname = ''
        self._deployment_config = None
        self._log_level = ''
        self._security_option = None
        self._force_no_passwords = False
        self._device_ca_passphrase = None
        self._agent_ca_passphrase = None
        self._device_ca_passphrase_file_path = ''
        self._agent_ca_passphrase_file_path = ''
        self._owner_ca_cert_file_path = ''
        self._device_ca_cert_file_path = ''
        self._device_ca_chain_cert_file_path = ''
        self._device_ca_private_key_file_path = ''
        self._cert_subject = {}

    @property
    def schema_version(self):
        return self._schema_version

    @schema_version.setter
    def schema_version(self, value):
        if value not in EdgeHostConfig._supported_schemas:
            raise ValueError('Unsupported schema version:' + str(value))
        self._schema_version = value

    @property
    def config_dir(self):
        return self._config_dir

    @config_dir.setter
    def config_dir(self, value):
        if value and value.strip() != '':
            self._config_dir = value
        else:
            raise ValueError('Invalid configuration directory: ' + value)

    @property
    def config_dir_source(self):
        return self._config_dir_source

    @config_dir_source.setter
    def config_dir_source(self, value):
        if isinstance(value, EdgeConfigDirInputSource):
            self._config_dir_source = value
        else:
            raise ValueError('Invalid configuration directory input source: ' + str(value))

    @property
    def home_dir(self):
        return self._home_dir

    @home_dir.setter
    def home_dir(self, value):
        if value and value.strip() != '':
            self._home_dir = value
        else:
            raise ValueError('Invalid home directory: ' + value)
        self._home_dir = value

    @property
    def connection_string(self):
        return self._connection_string

    @connection_string.setter
    def connection_string(self, value):
        is_valid = False
        if value is not None:
            pattern = re.compile(r'^\s*HostName=\S+;+DeviceId=\S+;+SharedAccessKey=\S+$')
            if pattern.match(value):
                is_valid = True
                self._connection_string = value
        if is_valid is False:
            raise ValueError('Invalid connection string: ' + str(value))

    @property
    def hostname(self):
        return self._hostname

    @hostname.setter
    def hostname(self, value):
        is_valid = False
        if value is not None:
            length = len(value)
            if length > 0 and length <= 64:
                self._hostname = value.lower()
                is_valid = True

        if is_valid is False:
            raise ValueError('Invalid hostname. Hostname cannot be empty or' \
                             + ' greater than 64 characters: ' + str(value))

    @property
    def log_level(self):
        return self._log_level

    @log_level.setter
    def log_level(self, value):
        is_valid = False
        if value in EdgeHostConfig._supported_log_levels:
            self._log_level = value
            is_valid = True

        if is_valid is False:
            raise ValueError('Invalid log level:' + str(value))

    @property
    def deployment_config(self):
        return self._deployment_config

    @deployment_config.setter
    def deployment_config(self, value):
        if value is not None:
            if value.type in EdgeDefault.get_supported_deployments(platform.system()):
                self._deployment_config = value
        else:
            raise ValueError('Invalid deployment config')

    @property
    def deployment_type(self):
        result = ''
        if self._deployment_config:
            result = self._deployment_config.type
        return result

    @property
    def certificate_subject_dict(self):
        return self._cert_subject

    def _merge(self, subj_dict):
        default_cert_subject = EdgeDefault.get_certificate_subject_dict()
        self._cert_subject = default_cert_subject.copy()
        if subj_dict:
            self._cert_subject.update(subj_dict)

    def _validate_passphrases(self, kwargs):
        try:
            is_valid_input = False

            args_list = list(kwargs.keys())
            dca_passphrase = None
            if 'device_ca_passphrase' in list(args_list) and \
                    kwargs['device_ca_passphrase']:
                dca_passphrase = kwargs['device_ca_passphrase']
            dca_passphrase_file = None
            if 'device_ca_passphrase_file' in list(args_list) and \
                    kwargs['device_ca_passphrase_file']:
                dca_passphrase_file = kwargs['device_ca_passphrase_file']
            agt_passphrase = None
            if 'agent_ca_passphrase' in list(args_list) and \
                    kwargs['agent_ca_passphrase']:
                agt_passphrase = kwargs['agent_ca_passphrase']
            agt_passphrase_file = None
            if 'agent_ca_passphrase_file' in list(args_list) and \
                    kwargs['agent_ca_passphrase_file']:
                agt_passphrase_file = kwargs['agent_ca_passphrase_file']

            if dca_passphrase and dca_passphrase_file:
                log.error('Passphrase and passphrase file both cannot be set' \
                          ' for Device CA private key')
            elif agt_passphrase and agt_passphrase_file:
                log.error('Passphrase and passphrase file both cannot be set' \
                          ' for Agent CA private key')
            else:
                if dca_passphrase:
                    dev_ca_pass = dca_passphrase
                elif dca_passphrase_file:
                    pass_file = dca_passphrase_file
                    self.device_ca_passphrase_file_path = pass_file
                    with open(pass_file, 'r') as ip_file:
                        dev_ca_pass = ip_file.read().rstrip()
                else:
                    dev_ca_pass = None

                if agt_passphrase:
                    ag_ca_pass = agt_passphrase
                elif agt_passphrase_file:
                    pass_file = agt_passphrase_file
                    self.agent_ca_passphrase_file_path = pass_file
                    with open(pass_file, 'r') as ip_file:
                        ag_ca_pass = ip_file.read().rstrip()
                else:
                    ag_ca_pass = None

                if ag_ca_pass and self._force_no_passwords:
                    log.error('Inconsistent password options. Force no passwords' \
                              ' was specified and an Agent CA passphrase was provided.')
                elif dev_ca_pass and self._force_no_passwords and \
                        self._security_option == EdgeHostConfig.security_option_self_signed:
                    log.error('Inconsistent password options. Force no passwords' \
                              ' was specified and a Device CA passphrase was provided.')
                else:
                    self._agent_ca_passphrase = ag_ca_pass
                    self._device_ca_passphrase = dev_ca_pass
                    is_valid_input = True

            return is_valid_input

        except IOError as ex_os:
            log.error('Error reading file: %s. Errno: %s, Error %s',
                      pass_file, str(ex_os.errno), ex_os.strerror)
            raise edgectl.errors.EdgeFileAccessError('Cannot read file', pass_file)


    def set_security_options(self, force_no_passwords, subject_dict, **kwargs):
        """
        Validate and set the security options pertaining to Edge
        certificate provisioning

        Args:
            force_no_passwords (bool): Bypass private key password prompts
            subject_dict (dict):
              edgectl.edgeconstants.SUBJECT_COUNTRY_KEY: 2 letter country code
              edgectl.edgeconstants.SUBJECT_STATE_KEY: state
              edgectl.edgeconstants.SUBJECT_LOCALITY_KEY: locality/city
              edgectl.edgeconstants.SUBJECT_ORGANIZATION_KEY: organization
              edgectl.edgeconstants.SUBJECT_ORGANIZATION_UNIT_KEY: organization unit
              edgectl.edgeconstants.SUBJECT_COMMON_NAME_KEY: device CA common name
            kwargs:
              owner_ca_cert_file (str): Path to Owner CA PEM formatted certificate file
              device_ca_cert_file (str): Path to Device CA PEM formatted certificate file
              device_ca_chain_cert_file (str): Path to Device CA Chain PEM formatted certificate file
              device_ca_private_key_file (str): Path to Device CA Private Key PEM formatted file
              device_ca_passphrase (str): Passphrase in ascii to read the Device CA private key
              device_ca_passphrase_file (str): Path to a file containing passphrase in ascii
                                               to read the Device CA private key
              agent_ca_passphrase (str): Passphrase in ascii to use when generating
                                         the Edge Agent CA certificate
              agent_ca_passphrase_file (str): Path to a file containing passphrase in ascii
                                              to use when generating the Edge Agent CA certificate

        Raises:
            edgectl.errors.EdgeFileAccessError - Reporting any file access errors
            ValueError - Any input found to be invalid
        """
        security_option = None
        is_valid_input = False
        self._force_no_passwords = force_no_passwords

        dca_keys_list = ['owner_ca_cert_file',
                         'device_ca_cert_file',
                         'device_ca_chain_cert_file',
                         'device_ca_private_key_file']
        count = 0
        for key in list(dca_keys_list):
            if key in list(kwargs.keys()):
                if kwargs[key]:
                    count += 1
        security_option = None

        if count == len(dca_keys_list):
            # all required pre installed data inputs were provided
            security_option = EdgeHostConfig.security_option_pre_installed
        elif count == 0:
            # no pre installed data inputs were provided generate self signed certs
            security_option = EdgeHostConfig.security_option_self_signed
        else:
            log.error('Incorrect input data provided when' \
                      ' registering Device CA certificate.\n' \
                      'When registering the Device CA certificate,' \
                      ' the following should be provided:\n'\
                      ' - Device CA certificate file\n' \
                      ' - Device CA''s private key file and it''s passphrase (if any)\n' \
                      ' - Owner CA certificate file\n' \
                      ' - Owner CA to Device CA chain certificate file\n')
        if security_option:
            log.debug('User certificate option: %s', security_option)
            self._security_option = security_option
            self._merge(subject_dict)
            country = self._cert_subject[EC.SUBJECT_COUNTRY_KEY]
            self._cert_subject[EC.SUBJECT_COUNTRY_KEY] = country.upper()
            if self._security_option == EdgeHostConfig.security_option_self_signed:
                if len(country) != 2:
                    msg = 'Invalid certificate country code {0}. Length should be 2 characters.'.format(country)
                    log.error(msg)
                    raise ValueError(msg)
            else:
                self.owner_ca_cert_file_path = kwargs['owner_ca_cert_file']
                self.device_ca_cert_file_path = kwargs['device_ca_cert_file']
                self.device_ca_chain_cert_file_path = kwargs['device_ca_chain_cert_file']
                self.device_ca_private_key_file_path = kwargs['device_ca_private_key_file']
            is_valid_input = self._validate_passphrases(kwargs)
        if is_valid_input is False:
            raise ValueError('Incorrect certificate options provided')

    @property
    def security_option(self):
        return self._security_option

    @security_option.setter
    def security_option(self, value):
        is_valid = False
        if value in EdgeHostConfig._supported_security_options:
            self._security_option = value
            is_valid = True

        if is_valid is False:
            raise ValueError('Invalid security option:' + str(value))

    def use_self_signed_certificates(self):
        return (self._security_option == EdgeHostConfig.security_option_self_signed)

    @staticmethod
    def _check_file_path_validity(value):
        if value is None or os.path.exists(value) is False \
            or os.path.isfile(value) is False:
            return False
        return True

    @property
    def owner_ca_cert_file_path(self):
        return self._owner_ca_cert_file_path

    @owner_ca_cert_file_path.setter
    def owner_ca_cert_file_path(self, value):
        if self._check_file_path_validity(value):
            self._owner_ca_cert_file_path = os.path.realpath(value)
        else:
            raise ValueError('Invalid Owner CA cert file:' + str(value))

    @property
    def device_ca_cert_file_path(self):
        return self._device_ca_cert_file_path

    @device_ca_cert_file_path.setter
    def device_ca_cert_file_path(self, value):
        if self._check_file_path_validity(value):
            self._device_ca_cert_file_path = os.path.realpath(value)
        else:
            raise ValueError('Invalid Device CA cert file:' + str(value))

    @property
    def device_ca_chain_cert_file_path(self):
        return self._device_ca_chain_cert_file_path

    @device_ca_chain_cert_file_path.setter
    def device_ca_chain_cert_file_path(self, value):
        if self._check_file_path_validity(value):
            self._device_ca_chain_cert_file_path = os.path.realpath(value)
        else:
            raise ValueError('Invalid Device CA Chain cert file:' + str(value))

    @property
    def device_ca_private_key_file_path(self):
        return self._device_ca_private_key_file_path

    @device_ca_private_key_file_path.setter
    def device_ca_private_key_file_path(self, value):
        if self._check_file_path_validity(value):
            self._device_ca_private_key_file_path = os.path.realpath(value)
        else:
            raise ValueError('Invalid Device CA private key cert file:' + str(value))

    @property
    def device_ca_passphrase(self):
        return self._device_ca_passphrase

    @property
    def agent_ca_passphrase(self):
        return self._agent_ca_passphrase

    @property
    def device_ca_passphrase_file_path(self):
        return self._device_ca_passphrase_file_path

    @device_ca_passphrase_file_path.setter
    def device_ca_passphrase_file_path(self, value):
        if self._check_file_path_validity(value):
            self._device_ca_passphrase_file_path = os.path.realpath(value)
        else:
            raise ValueError('Invalid Device CA passphrase file:' + str(value))

    @property
    def agent_ca_passphrase_file_path(self):
        return self._agent_ca_passphrase_file_path

    @agent_ca_passphrase_file_path.setter
    def agent_ca_passphrase_file_path(self, value):
        if self._check_file_path_validity(value):
            self._agent_ca_passphrase_file_path = os.path.realpath(value)
        else:
            raise ValueError('Invalid Agent CA passphrase file:' + str(value))

    @property
    def force_no_passwords(self):
        return self._force_no_passwords

    @force_no_passwords.setter
    def force_no_passwords(self, value):
        self._force_no_passwords = value

    def _cert_subject_str(self):
        result = ''
        output_keys = [EC.SUBJECT_COUNTRY_KEY, EC.SUBJECT_STATE_KEY,
                       EC.SUBJECT_LOCALITY_KEY, EC.SUBJECT_ORGANIZATION_KEY,
                       EC.SUBJECT_ORGANIZATION_UNIT_KEY,
                       EC.SUBJECT_COMMON_NAME_KEY]
        input_keys = list(self._cert_subject.keys())
        input_keys_len = len(input_keys)
        new_line_idx = 0
        for output_key in output_keys:
            if output_key in input_keys:
                if new_line_idx != 0:
                    result += ', '
                else:
                    result += '\n\t\t\t'
                new_line_idx = (new_line_idx + 1) % 3
                result += output_key + ': ' + self._cert_subject[output_key]
        return result

    def _sanitize_conn_str(self, conn_str):
        try:
            items = [(s[0], s[1] if s[0].lower() != 'sharedaccesskey' else '******') \
                    for s in [p.split('=', 2) for p in conn_str.split(';')]]
            return ';'.join(["%s=%s" % p for p in items])
        except:
            return '******'

    def __str__(self):
        result = 'Schema Version:\t\t' + self._schema_version + '\n'
        result += 'Connection String:\t' + self._sanitize_conn_str(self.connection_string) + '\n'
        result += 'Config Directory:\t' + self.config_dir + '\n'
        result += 'Home Directory:\t\t' + self.home_dir + '\n'
        result += 'Hostname:\t\t' + self.hostname + '\n'
        result += 'Log Level:\t\t' + self.log_level + '\n'
        result += 'Security Option:\t' + self.security_option + '\n'
        result += 'Force No Passwords:\t' + str(self.force_no_passwords) + '\n'
        if self.use_self_signed_certificates() is False:
            result += 'Owner CA Cert File:\t\n'
            result += '\t\t\t' + str(self.owner_ca_cert_file_path) + '\n'
            result += 'Device CA Cert File:\t\n'
            result += '\t\t\t' + str(self.device_ca_cert_file_path) + '\n'
            result += 'Device CA Chain Cert File:\t\n'
            result += '\t\t\t' + str(self.device_ca_chain_cert_file_path) + '\n'
            result += 'Device CA Private Key File:\t\n'
            result += '\t\t\t' + str(self.device_ca_private_key_file_path) + '\n'

        if self.device_ca_passphrase_file_path != '':
            result += 'Device CA Passphrase File:\n'
            result += '\t\t\t' + str(self.device_ca_passphrase_file_path) + '\n'
        if self.agent_ca_passphrase_file_path != '':
            result += 'Agent CA Passphrase File:\n'
            result += '\t\t\t' + str(self.agent_ca_passphrase_file_path) + '\n'
        if self._security_option == EdgeHostConfig.security_option_self_signed:
            result += 'Certificate Subject:\t' + self._cert_subject_str() + '\n'
        if self.deployment_config:
            result += str(self.deployment_config)
        return result

    def _security_to_dict(self):
        certs_dict = {}
        if self.use_self_signed_certificates():
            # handle self signed cert options
            security_opt_selfsigned = {}
            security_opt_selfsigned[EC.FORCENOPASSWD_KEY] = \
                self.force_no_passwords
            security_opt_selfsigned[EC.DEVICE_CA_PASSPHRASE_FILE_KEY] = \
                self.device_ca_passphrase_file_path
            security_opt_selfsigned[EC.AGENT_CA_PASSPHRASE_FILE_KEY] = \
                self.agent_ca_passphrase_file_path
            certs_dict[EC.CERTS_OPTION_KEY] = EC.SELFSIGNED_KEY
            certs_dict[EC.SELFSIGNED_KEY] = security_opt_selfsigned
        else:
            # pre installed cert options
            security_opt_preinstalled = {}
            security_opt_preinstalled[EC.PREINSTALL_OWNER_CA_CERT_KEY] = \
                self.owner_ca_cert_file_path
            security_opt_preinstalled[EC.PREINSTALL_DEVICE_CERT_KEY] = \
                self.device_ca_cert_file_path
            security_opt_preinstalled[EC.PREINSTALL_DEVICE_CHAINCERT_KEY] = \
                self.device_ca_chain_cert_file_path
            security_opt_preinstalled[EC.PREINSTALL_DEVICE_PRIVKEY_KEY] = \
                self.device_ca_private_key_file_path
            security_opt_preinstalled[EC.DEVICE_CA_PASSPHRASE_FILE_KEY] = \
                self.device_ca_passphrase_file_path
            security_opt_preinstalled[EC.AGENT_CA_PASSPHRASE_FILE_KEY] = \
                self.agent_ca_passphrase_file_path
            security_opt_preinstalled[EC.FORCENOPASSWD_KEY] = \
                self.force_no_passwords
            certs_dict[EC.CERTS_OPTION_KEY] = EC.PREINSTALL_KEY
            certs_dict[EC.PREINSTALL_KEY] = security_opt_preinstalled
        certs_dict[EC.CERTS_SUBJECT_KEY] = self._cert_subject
        security_dict = {}
        security_dict[EC.CERTS_KEY] = certs_dict
        return security_dict

    def to_dict(self):
        d = {}
        d[EC.SCHEMA_KEY] = self._schema_version
        d[EC.DEVICE_CONNECTION_STRING_KEY] = self.connection_string
        if self.config_dir_source == EdgeConfigDirInputSource.USER_PROVIDED:
            d[EC.CONFIG_DIR_KEY] = self.config_dir
        d[EC.HOMEDIR_KEY] = self.home_dir
        d[EC.HOSTNAME_KEY] = self.hostname
        d[EC.EDGE_RUNTIME_LOG_LEVEL_KEY] = self.log_level
        d[EC.SECURITY_KEY] = self._security_to_dict()
        deployment_dict = {}
        deployment_dict[EC.DEPLOYMENT_TYPE_KEY] = self.deployment_type
        deployment_dict[self.deployment_type] = self.deployment_config.to_dict()
        d[EC.DEPLOYMENT_KEY] = deployment_dict
        return d

    def to_json(self):
        d = self.to_dict()
        return json.dumps(d, indent=2, sort_keys=True)
