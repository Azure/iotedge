""" This module implements functionality to validate and store configuration
    data required to configure and operate the IoT Edge.
"""
import platform
import re
from edgectl.config.edgeconstants import EdgeConfigDirInputSource
from edgectl.config.edgeconstants import EdgeUpstreamProtocol
from edgectl.config.edgeconstants import EdgeConstants as EC
from edgectl.config.default import EdgeDefault
from edgectl.config.configbase import EdgeConfigBase
from edgectl.config.certconfig import EdgeCertConfig
from edgectl.config.configbase import EdgeDeploymentConfig
from edgectl.utils import EdgeUtils


class EdgeHostConfig(EdgeConfigBase):
    """
        This class implements APIs to validate and store and retrieve
        configuration data required to setup and operate the IoT Edge.
    """
    _supported_schemas = ['1']
    _default_schema_version = '1'

    def __init__(self):
        super(EdgeHostConfig, self).__init__()
        self._config_dict = {
            EC.SCHEMA_KEY: EdgeHostConfig._default_schema_version,
            EC.HOMEDIR_KEY: '',
            EC.CONFIG_DIR_KEY: '',
            EC.CONFIG_DIR_SOURCE_KEY: EdgeConfigDirInputSource.NONE,
            EC.HOSTNAME_KEY: '',
            EC.DEVICE_CONNECTION_STRING_KEY: '',
            EC.EDGE_RUNTIME_LOG_LEVEL_KEY: EdgeDefault.get_default_runtime_log_level(),
            EC.DEPLOYMENT_KEY: None,
            EC.CERTS_KEY: None,
            EC.UPSTREAM_PROTOCOL: None
        }

    @property
    def schema_version(self):
        """Getter for schema version returned as a string."""
        return self._config_dict[EC.SCHEMA_KEY]

    @schema_version.setter
    def schema_version(self, value):
        """Setter for schema version
        Args:
            value (str): Schema version
        Raises:
            ValueError if value is None or set to an invalid version
        """
        if value is not None and value in EdgeHostConfig._supported_schemas:
            self._config_dict[EC.SCHEMA_KEY] = value
        else:
            raise ValueError('Unsupported schema version: {0}'.format(value))

    @property
    def config_dir(self):
        """Getter for IoT Edge config directory path returned as a string."""
        return self._config_dict[EC.CONFIG_DIR_KEY]

    @config_dir.setter
    def config_dir(self, value):
        """Setter for IoT Edge config directory path
        Args:
            value (str): Dir path
        Raises:
            ValueError if value is None or empty
        """
        is_valid = False
        if value:
            value = value.strip()
            if value != '':
                self._config_dict[EC.CONFIG_DIR_KEY] = value
                is_valid = True
        if is_valid is False:
            raise ValueError('Invalid configuration directory: {0}'.format(value))

    @property
    def config_dir_source(self):
        """ Getter for IoT Edge config directory path input source enum
            edgectl.config.EdgeConfigDirInputSource
        """
        return self._config_dict[EC.CONFIG_DIR_SOURCE_KEY]

    @config_dir_source.setter
    def config_dir_source(self, value):
        """Setter for IoT Edge config directory path input source
        Args:
            value (edgectl.config.EdgeConfigDirInputSource): Input source
        Raises:
            ValueError is not a type of edgectl.config.EdgeConfigDirInputSource
        """
        if isinstance(value, EdgeConfigDirInputSource):
            self._config_dict[EC.CONFIG_DIR_SOURCE_KEY] = value
        else:
            raise ValueError('Invalid configuration directory input source: {0}'.format(value))

    @property
    def upstream_protocol(self):
        """ Getter for upstream protocol
            edgectl.config.EdgeUpstreamProtocol
        """
        return self._config_dict[EC.UPSTREAM_PROTOCOL]

    @upstream_protocol.setter
    def upstream_protocol(self, value):
        """Setter for upstream protocol
        Args:
            value (edgectl.config.EdgeUpstreamProtocol): upstream protocol
        Raises:
            ValueError is not a type of edgectl.config.EdgeUpstreamProtocol
        """
        try:
            self._config_dict[EC.UPSTREAM_PROTOCOL] = EdgeUpstreamProtocol(value)
        except:
            raise ValueError('Invalid upstream protocol: {0}'.format(value))

    @property
    def home_dir(self):
        """Getter for IoT Edge home directory path returned as a string."""
        return self._config_dict[EC.HOMEDIR_KEY]

    @home_dir.setter
    def home_dir(self, value):
        """Setter for IoT Edge home directory path
        Args:
            value (str): Dir path
        Raises:
            ValueError if value is None or empty
        """
        is_valid = False
        if value is not None:
            value = value.strip()
            if value != '':
                self._config_dict[EC.HOMEDIR_KEY] = value
                is_valid = True
        if is_valid is False:
            raise ValueError('Invalid home directory: {0}'.format(value))

    @property
    def connection_string(self):
        """Getter for IoT Hub connection string returned as a string."""
        return self._config_dict[EC.DEVICE_CONNECTION_STRING_KEY]

    @connection_string.setter
    def connection_string(self, value):
        """Setter for IoT Hub connection string
        Args:
            value (str): Connection string
        Raises:
            ValueError if value is None or not format compliant
        """
        is_valid = False
        if value is not None:
            value = value.strip()
            pattern = re.compile(r'^HostName=\S+;+DeviceId=\S+;+SharedAccessKey=\S+$')
            if pattern.match(value):
                is_valid = True
                self._config_dict[EC.DEVICE_CONNECTION_STRING_KEY] = value
        if is_valid is False:
            raise ValueError('Invalid connection string: {0}'.format(value))

    @property
    def hostname(self):
        """Getter for IoT Edge hostname returned as a string."""
        return self._config_dict[EC.HOSTNAME_KEY]

    @hostname.setter
    def hostname(self, value):
        """Setter for IoT Edge hostname.
        Note: Hostname will be saved in lowercase ascii

        Args:
            value (str): IoT Edge hostname

        Raises:
            ValueError if value is None, empty or greater than 64 characters.
        """
        is_valid = False
        msg = 'Invalid hostname. Hostname cannot be empty or ' \
              'greater than 64 characters: {0}'.format(value)
        if value is not None:
            value = value.strip().lower()
            length = len(value)
            if length > 0 and length <= 64:
                if value in ['localhost', '127.0.0.1', '::1', '0.0.0.0', '::0']:
                    msg = 'Hostname cannot be "localhost" or any of its variant IP addresses.' \
                          'The hostname must be a valid DNS (or machine) name.'
                else:
                    self._config_dict[EC.HOSTNAME_KEY] = value
                    is_valid = True

        if is_valid is False:
            raise ValueError(msg)

    @property
    def log_level(self):
        """Getter for IoT Edge runtime log level as a string."""
        return self._config_dict[EC.EDGE_RUNTIME_LOG_LEVEL_KEY]

    @log_level.setter
    def log_level(self, value):
        """Setter for IoT Edge runtime log levels. The supported log levels
        can be determined from EdgeDefault.get_runtime_log_levels()

        Args:
            value (str): Log level

        Raises:
            ValueError if value is None or not a supported level.
        """
        is_valid = False
        if value is not None:
            value = value.strip()
            if value in EdgeDefault.get_runtime_log_levels():
                self._config_dict[EC.EDGE_RUNTIME_LOG_LEVEL_KEY] = value
                is_valid = True

        if is_valid is False:
            raise ValueError('Invalid log level: {0}'.format(value))

    @property
    def deployment_config(self):
        """ Getter for IoT Edge runtime deployment configuration.

            Returns:
                An EdgeDeploymentConfig object containing the deployment
                specific configuration.
        """
        return self._config_dict[EC.DEPLOYMENT_KEY]

    @deployment_config.setter
    def deployment_config(self, value):
        """
        Setter for IoT Edge runtime deployment configuration.

        Args:
            value (EdgeDeploymentConfig): A deployment configuration object

        Raises:
            ValueError if value is not an instance of EdgeDeploymentConfig or
            if the object's deployment type is unsupported.
        """
        is_valid = False
        if value and isinstance(value, EdgeDeploymentConfig):
            deployments = EdgeDefault.get_supported_deployments(platform.system())
            if value.deployment_type in deployments:
                self._config_dict[EC.DEPLOYMENT_KEY] = value
                is_valid = True

        if is_valid is False:
            raise ValueError('Invalid deployment configuration')

    @property
    def deployment_type(self):
        """ Getter for IoT Edge runtime deployment type.

            Returns:
                String specifying the deployment type
                    'docker': for Docker based Edge deployments
                    None otherwise
        """
        result = None
        deployment_config = self.deployment_config
        if deployment_config:
            result = deployment_config.deployment_type
        return result

    @property
    def certificate_config(self):
        """ Getter for IoT Edge runtime certificate configuration.

            Returns:
                An EdgeCertConfig object containing the deployment
                specific configuration.
        """
        return self._config_dict[EC.CERTS_KEY]

    @certificate_config.setter
    def certificate_config(self, value):
        """
        Setter for IoT Edge runtime certificate configuration.

        Args:
            value (EdgeCertConfig): A deployment configuration object

        Raises:
            ValueError if value is not an instance of EdgeCertConfig.
        """
        if value and isinstance(value, EdgeCertConfig):
            self._config_dict[EC.CERTS_KEY] = value
        else:
            raise ValueError('Invalid Edge certificate configuration')

    def __str__(self):
        result = 'Schema Version:\t\t' + self.schema_version + '\n'
        conn_str = EdgeUtils.sanitize_connection_string(self.connection_string)
        result += 'Connection String:\t' + conn_str + '\n'
        result += 'Config Directory:\t' + self.config_dir + '\n'
        result += 'Home Directory:\t\t' + self.home_dir + '\n'
        result += 'Hostname:\t\t' + self.hostname + '\n'
        result += 'Log Level:\t\t' + self.log_level + '\n'
        if self.certificate_config:
            result += str(self.certificate_config)
        if self.deployment_config:
            result += str(self.deployment_config)
        return result

    def to_dict(self):
        """ Return a dict representation of the Edge config """
        result = {}
        result[EC.SCHEMA_KEY] = self.schema_version
        result[EC.DEVICE_CONNECTION_STRING_KEY] = self.connection_string
        if self.config_dir_source == EdgeConfigDirInputSource.USER_PROVIDED:
            result[EC.CONFIG_DIR_KEY] = self.config_dir
        result[EC.HOMEDIR_KEY] = self.home_dir
        result[EC.HOSTNAME_KEY] = self.hostname
        result[EC.EDGE_RUNTIME_LOG_LEVEL_KEY] = self.log_level
        if self.certificate_config:
            result[EC.SECURITY_KEY] = self.certificate_config.to_dict()
        deployment_dict = {}
        deployment_dict[EC.DEPLOYMENT_TYPE_KEY] = self.deployment_type
        if self.deployment_config:
            deployment_dict[self.deployment_type] = self.deployment_config.to_dict()
        result[EC.DEPLOYMENT_KEY] = deployment_dict
        if self.upstream_protocol is not None and self.upstream_protocol != EdgeUpstreamProtocol.NONE:
            result[EC.UPSTREAM_PROTOCOL] = self.upstream_protocol.value
        return result
