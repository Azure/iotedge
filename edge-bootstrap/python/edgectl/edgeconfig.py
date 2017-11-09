import json
import logging as log
import os
import re
import edgectl.edgeconstants as EC
from edgectl.edgeutils import EdgeUtils
from edgectl.default  import EdgeDefault


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
        result  = 'Deployment Type:\t' + self.type + '\n'
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
        logs_dict[EC.DOCKER_LOGGING_DRIVER_OPTIONS_KEY] = self.logging_options
        d[EC.DOCKER_LOGGING_OPTIONS_KEY] = logs_dict
        return d

class EdgeHostConfig(object):
    _supported_schemas = ['1']
    _default_schema_version = '1'
    _supported_log_levels = ['info', 'debug']
    security_option_self_signed = 'selfSigned'
    security_option_pre_installed = 'preInstalled'
    _supported_security_options = [security_option_self_signed,
                                   security_option_pre_installed]

    def __init__(self):
        self._schema_version = EdgeHostConfig._default_schema_version
        self._home_dir = ''
        self._connection_string = ''
        self._hostname = ''
        self._deployment_config = None
        self._log_level = ''
        self._security_option = ''
        self._self_signed_cert_option_force_regen = False
        self._self_signed_cert_option_force_no_passwords = False
        self._ca_cert_path = ''
        self._edge_server_cert_path = ''

    @property
    def schema_version(self):
        return self._schema_version

    @schema_version.setter
    def schema_version(self, value):
        if value not in EdgeHostConfig._supported_schemas:
            raise ValueError('Unsupported schema version:' + str(value))
        self._schema_version = value

    @property
    def home_dir(self):
        return self._home_dir

    @home_dir.setter
    def home_dir(self, value):
        EdgeUtils.mkdir_if_needed(value)
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
            if value.type in EdgeDefault.get_supported_deployments():
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

    @property
    def ca_cert_path(self):
        return self._ca_cert_path

    @ca_cert_path.setter
    def ca_cert_path(self, value):
        if value is None \
            or os.path.exists(value) is False \
            or os.path.isfile(value) is False:
            raise ValueError('Invalid CA cert file:' + str(value))
        self._ca_cert_path = value

    @property
    def edge_server_cert_path(self):
        return self._edge_server_cert_path

    @edge_server_cert_path.setter
    def edge_server_cert_path(self, value):
        if value is None \
            or os.path.exists(value) is False \
            or os.path.isfile(value) is False:
            raise ValueError('Invalid CA cert file:' + str(value))
        self._edge_server_cert_path = value

    @property
    def self_signed_cert_option_force_regen(self):
        return self._self_signed_cert_option_force_regen

    @self_signed_cert_option_force_regen.setter
    def self_signed_cert_option_force_regen(self, value):
        self._self_signed_cert_option_force_regen = value

    @property
    def self_signed_cert_option_force_no_passwords(self):
        return self._self_signed_cert_option_force_no_passwords

    @self_signed_cert_option_force_no_passwords.setter
    def self_signed_cert_option_force_no_passwords(self, value):
        self._self_signed_cert_option_force_no_passwords = value

    def _sanitize_conn_str(self, conn_str):
        try:
            items = [(s[0], s[1] if s[0].lower() != 'sharedaccesskey' else '******') \
                    for s in map(lambda p: p.split('=', 2), conn_str.split(';'))]
            return ';'.join(map(lambda p: "%s=%s" % p, items))
        except:
            return '******'

    def __str__(self):
        result  = 'Schema Version:\t\t' + self._schema_version + '\n'
        result += 'Connection String:\t' + self._sanitize_conn_str(self.connection_string) + '\n'
        result += 'Home Directory:\t\t' + self.home_dir + '\n'
        result += 'Hostname:\t\t' + self.hostname + '\n'
        result += 'Log Level:\t\t' + self.log_level + '\n'
        result += 'Security Option:\t' + self.security_option + '\n'
        if self.deployment_config:
            result += str(self.deployment_config)
        return result

    def __security_to_dict(self):
        # handle self signed cert options
        security_opt_selfsigned = {}
        security_opt_selfsigned[EC.SELFSIGNED_FORCEREGEN_KEY] = \
            self._self_signed_cert_option_force_regen
        security_opt_selfsigned[EC.SELFSIGNED_FORCENOPASSWD_KEY] = \
            self._self_signed_cert_option_force_no_passwords
        # pre installed cert options
        security_opt_preinstalled = {}
        security_opt_preinstalled[EC.PREINSTALLED_DEVICE_CA_CERT_KEY] = \
            self._ca_cert_path
        security_opt_preinstalled[EC.PREINSTALLED_SERVER_CERT_KEY] = \
            self._edge_server_cert_path
        certs_dict = {}
        # @todo add support for preinstalled
        certs_dict[EC.CERTS_OPTION_KEY] = EC.SELFSIGNED_KEY
        certs_dict[EC.SELFSIGNED_KEY] = security_opt_selfsigned
        certs_dict[EC.PREINSTALLED_KEY] = security_opt_preinstalled
        security_dict = {}
        security_dict[EC.CERTS_KEY] = certs_dict
        return security_dict

    def to_dict(self):
        d = {}
        d[EC.SCHEMA_KEY] = self._schema_version
        d[EC.DEVICE_CONNECTION_STRING_KEY] = self.connection_string
        d[EC.HOMEDIR_KEY] = self.home_dir
        d[EC.HOSTNAME_KEY] = self.hostname
        d[EC.EDGE_RUNTIME_LOG_LEVEL_KEY] = self.log_level
        d[EC.SECURITY_KEY] = self.__security_to_dict()
        deployment_dict = {}
        deployment_dict[EC.DEPLOYMENT_TYPE_KEY] = self.deployment_type
        deployment_dict[self.deployment_type] = self.deployment_config.to_dict()
        d[EC.DEPLOYMENT_KEY] = deployment_dict
        return d

    def to_json(self):
        d = self.to_dict()
        return json.dumps(d, indent=2, sort_keys=True)
