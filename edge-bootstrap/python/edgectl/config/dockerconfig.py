""" This module implements functionality to validate and store configuration
    data of the installed Docker environment on the host.
"""
import logging as log
import re
from edgectl.config.configbase import EdgeDeploymentConfig
from edgectl.config.edgeconstants import EdgeConstants as EC
from edgectl.utils import EdgeUtils

PROTOCOL_KEY = 'protocol'
PORT_KEY = 'port'
ENDPOINT_KEY = 'endpoint'
URI_KEY = 'uri'
IMAGE_KEY = 'image'
IMAGE_REPOSITORY_KEY = 'repository'
IMAGE_NAME_KEY = 'name'
IMAGE_TAG_KEY = 'tag'

class EdgeDeploymentConfigDocker(EdgeDeploymentConfig):
    """
        This class implements APIs to validate and store and retrieve
        configuration data required to setup and operate the IoT Edge
        using Docker.
    """
    def __init__(self):
        super(EdgeDeploymentConfigDocker, self).__init__(EC.DEPLOYMENT_DOCKER_KEY)
        self._uri_dict = {URI_KEY: '', PROTOCOL_KEY: '', ENDPOINT_KEY: '', PORT_KEY: ''}
        self._edge_image_dict = {IMAGE_KEY: '', IMAGE_REPOSITORY_KEY: '',
                                 IMAGE_NAME_KEY: '', IMAGE_TAG_KEY: ''}
        self._registries = []
        self._logging_driver = ''
        self._logging_options_dict = {}

    @property
    def uri(self):
        """Getter for the docker URI returned as a string."""
        return self._uri_dict[URI_KEY]

    @property
    def uri_endpoint(self):
        """
            Getter for the docker URI endpoint [protocol://][endpoint]:[port]
            returned as a string.
        """
        return self._uri_dict[ENDPOINT_KEY]

    @property
    def uri_port(self):
        """
            Getter for the docker URI port [protocol://][endpoint]:[port]
            returned as a string.
            If a port is not applicable '' is returned.
        """
        return self._uri_dict[PORT_KEY]

    @property
    def uri_protocol(self):
        """
            Getter for the docker URI protocol [protocol://][endpoint]:[port]
            returned as a string.
        """
        return self._uri_dict[PROTOCOL_KEY]

    @uri.setter
    def uri(self, value):
        """
        Setter for docker URI

        Args:
            value (str): Docker URI
            Typical formats: [protocol://][endpoint]:[port]
                             [protocol://][endpoint]
        Raises:
            ValueError if value is None or is not format compliant
        """
        is_valid = False
        protocol = ''
        endpoint = ''
        port = ''
        uri_str = ''
        if value is not None:
            value = value.strip()
            length = len(value)
            if length > 0:
                pattern = re.compile(r'^\s*([a-z]+://)(\S+)$')
                match_obj = pattern.match(value)
                if match_obj:
                    protocol = match_obj.group(1)
                    if protocol in ['http://', 'https://', 'tcp://']:
                        pattern = re.compile(r'^\s*([a-z]+://)(\S+):([0-9]+)$')
                        match_obj = pattern.match(value)
                        if match_obj:
                            endpoint = match_obj.group(2)
                            port = match_obj.group(3)
                            uri_str = value
                            is_valid = True
                    elif protocol in ['npipe://']:
                        # Docker engine on Windows is at a well-known path.
                        endpoint = EC.DOCKER_ENGINE_WINDOWS_ENDPOINT
                        port = ''
                        uri_str = value
                        is_valid = True
                    elif protocol in ['unix://']:
                        pattern = re.compile(r'^\s*([a-z]+://)(\S+)$')
                        match_obj = pattern.match(value)
                        if match_obj:
                            endpoint = match_obj.group(2)
                            port = ''
                            uri_str = value
                            is_valid = True
        if is_valid is True:
            self._uri_dict[PROTOCOL_KEY] = protocol
            self._uri_dict[ENDPOINT_KEY] = endpoint
            self._uri_dict[PORT_KEY] = port
            self._uri_dict[URI_KEY] = uri_str
        else:
            raise ValueError('Invalid docker URI: {0}'.format(value))

    @property
    def edge_image(self):
        """Getter for the IoT Edge runtime image returned as a string."""
        return self._edge_image_dict[IMAGE_KEY]

    @edge_image.setter
    def edge_image(self, value):
        """
        Setter for the IoT Edge runtime image

        Args:
            value (str): Edge runtime image
            Typical formats:
                [repository]/[image-name]:[tag]
                [repository]/[image-name]/[sub-image-name]:[tag]
                Other than the tags. each of the fields can be any non space ascii characters
                Tag field can be alphanumeric, '.', '-'
        Raises:
            ValueError if value is None or is not format compliant
        """
        is_valid = False
        image = ''
        image_repository = ''
        image_name = ''
        image_tag = ''
        if value is not None:
            value = value.strip()
            length = len(value)
            if length > 0:
                pattern = re.compile(r'^\s*([^\r\n\t\f /]+)/(\S+):([a-zA-Z0-9\.-]+)$')
                match_obj = pattern.match(value)
                if match_obj:
                    is_valid = True
                    image = value
                    log.debug('Found Edge Agent image: %s', image)
                    image_repository = match_obj.group(1)
                    log.debug('Found registry: %s', image_repository)
                    image_name = match_obj.group(2)
                    log.debug('Found image name: %s', image_name)
                    image_tag = match_obj.group(3)
                    log.debug('Found image tag: %s', image_tag)

        if is_valid is True:
            self._edge_image_dict[IMAGE_KEY] = image
            self._edge_image_dict[IMAGE_REPOSITORY_KEY] = image_repository
            self._edge_image_dict[IMAGE_NAME_KEY] = image_name
            self._edge_image_dict[IMAGE_TAG_KEY] = image_tag
        else:
            raise ValueError('Invalid Edge Agent image: {0}'.format(value))

    @property
    def edge_image_repository(self):
        """
            Getter for the IoT Edge runtime image's repository returned as a string.
            Ex. [repository]/[image-name]:[tag]
                [repository]/[image-name]/[sub-image-name]:[tag]
        """
        return self._edge_image_dict[IMAGE_REPOSITORY_KEY]

    @property
    def edge_image_name(self):
        """
            Getter for the IoT Edge runtime image's name (sub names) returned as a string.
            Ex. [repository]/[image-name]:[tag]
                [repository]/[image-name]/[sub-image-name]:[tag]
        """
        return self._edge_image_dict[IMAGE_NAME_KEY]

    @property
    def edge_image_tag(self):
        """
            Getter for the IoT Edge runtime image's tag returned as a string.
            Ex. [repository]/[image-name]:[tag]
                [repository]/[image-name]/[sub-image-name]:[tag]
        """
        return self._edge_image_dict[IMAGE_TAG_KEY]

    @property
    def registries(self):
        """Getter for the list of registries and their credentials"""
        return self._registries

    def _get_registry(self, server_address):
        for registry in self._registries:
            if registry[EC.REGISTRY_ADDRESS_KEY] == server_address:
                return registry
        return None

    def add_registry(self, server_address, username='', password=''):
        """
            API to add a registry and its credentials to the list of
            registries associated with a deployment.

        Args:
            server_address (str): Repository address
            username (str): Repository username
            password (str): Repository password

        Raises:
            ValueError if server address is None
        """
        if server_address is not None and username is not None and password is not None:
            server_address = server_address.strip()
            length = len(server_address)
            if length > 0:
                existing = self._get_registry(server_address)
                if existing is not None:
                    self._registries.remove(existing)
                registry = {EC.REGISTRY_ADDRESS_KEY: server_address,
                            EC.REGISTRY_USERNAME_KEY: username,
                            EC.REGISTRY_PASSWORD_KEY: password}
                self._registries.append(registry)
        else:
            raise ValueError('Invalid registry server address: {0}'.format(server_address))

    @property
    def logging_driver(self):
        """Getter for the Docker logging driver"""
        return self._logging_driver

    @logging_driver.setter
    def logging_driver(self, value):
        """
        Setter for the Docker logging driver. Ex. 'json-file', 'journald'.

        Args:
            value (str): Docker logging driver name

        Raises:
            ValueError if value is None or is empty
        """
        is_valid = False
        if value is not None:
            value = value.strip()
            length = len(value)
            if length > 0:
                self._logging_driver = value
                is_valid = True

        if is_valid is False:
            raise ValueError('Invalid docker logging driver: ' + str(value))

    @property
    def logging_options(self):
        """
        Getter for Docker logging options as a dictionary with the keys
        as the docker logging option and it's value as the docker logging
        option value.
        Ex. {'max-size': '10m'}
        """
        return self._logging_options_dict

    def add_logging_option(self, option_name, option_value):
        """
        Setter the Docker logging options.
        Ex. {'max-size': '10m'}

        Args:
            option_name (str): Docker logging option name
            option_value (str): Docker logging option value

        Raises:
            ValueError if either option_name or option_value is None or is empty
        """
        is_valid = False
        if option_name is not None and option_value is not None:
            option_name = option_name.strip()
            option_value = option_value.strip()
            len_key = len(option_name)
            len_val = len(option_value)
            if len_key > 0 and len_val > 0:
                if option_name not in self._logging_options_dict:
                    self._logging_options_dict[option_name] = option_value
                    is_valid = True

        if is_valid is False:
            raise ValueError('Invalid docker logging option: {0} {1}'.format(option_name,
                                                                             option_value))

    def __str__(self):
        result = 'Deployment Type:\t' + self.deployment_type + '\n'
        result += 'Docker Engine URI:\t' + self.uri + '\n'
        result += 'Edge Agent Image:\t' + self.edge_image + '\n'
        result += 'Registries:' + '\n'
        for registry in self.registries:
            result += '\t\t\t'
            reg_str = EdgeUtils.sanitize_registry_data(registry[EC.REGISTRY_ADDRESS_KEY],
                                                       registry[EC.REGISTRY_USERNAME_KEY],
                                                       registry[EC.REGISTRY_PASSWORD_KEY])
            result += reg_str + '\n'
        result += 'Logging Driver:\t\t' + self.logging_driver + '\n'
        options = self.logging_options
        for key in options:
            result += '\t\t\t' + str(key) + ': ' + options[key] + '\n'
        return result

    def to_dict(self):
        result = {}
        result[EC.DOCKER_URI_KEY] = self.uri
        result[EC.EDGE_RUNTIME_IMAGE_KEY] = self.edge_image
        result[EC.REGISTRIES_KEY] = self.registries
        logs_dict = {}
        logs_dict[EC.DOCKER_LOGGING_DRIVER_KEY] = self.logging_driver
        logs_dict[EC.DOCKER_LOGGING_DRIVER_OPTS_KEY] = self.logging_options
        result[EC.DOCKER_LOGGING_OPTS_KEY] = logs_dict
        return result
