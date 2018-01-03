"""
This module implements class EdgeDefault with APIs to
support various host OS and deployment specific configuration
required to operate the iotedgectl utility.
"""
import logging as log
import json
import os
import platform
import edgectl.errors
from edgectl.config.edgeconstants import EdgeConstants as EC


class EdgeDefault(object):
    """
    This class implements accessor APIs to OS and deployment
    specific configuration data.
    """
    _edge_dir = 'azure-iot-edge'
    _edge_config_file_name = 'config.json'
    _edge_meta_dir_name = '.iotedgectl'
    _edge_meta_config_file = 'config.json'
    _edge_ref_config_file = 'azure-iot-edge-config-reference.json'
    _edge_agent_dir_name = "__AzureIoTEdgeAgent"
    _edge_runtime_log_levels = [EC.EDGE_RUNTIME_LOG_LEVEL_INFO,
                                EC.EDGE_RUNTIME_LOG_LEVEL_DEBUG]
    _windows_config_path = os.getenv('PROGRAMDATA', '%%PROGRAMDATA%%')

    _cert_default_dict = {
        EC.SUBJECT_COUNTRY_KEY: 'US',
        EC.SUBJECT_STATE_KEY: 'Washington',
        EC.SUBJECT_LOCALITY_KEY: 'Redmond',
        EC.SUBJECT_ORGANIZATION_KEY: 'Default Edge Organization',
        EC.SUBJECT_ORGANIZATION_UNIT_KEY: 'Edge Unit',
        EC.SUBJECT_COMMON_NAME_KEY: 'Edge Device CA'
    }

    _platforms = {
        EC.DOCKER_HOST_LINUX: {
            'supported_deployments': [EC.DEPLOYMENT_DOCKER],
            'default_deployment': EC.DEPLOYMENT_DOCKER,
            'default_edge_conf_dir': '/etc/' + _edge_dir,
            'default_edge_data_dir': '/var/lib/' + _edge_dir,
            'default_edge_meta_dir_env': 'HOME',
            'deployment': {
                EC.DEPLOYMENT_DOCKER: {
                    EC.DOCKER_ENGINE_LINUX: {
                        'default_uri': 'unix:///var/run/docker.sock'
                    },
                }
            }
        },
        EC.DOCKER_HOST_WINDOWS: {
            'supported_deployments': [EC.DEPLOYMENT_DOCKER],
            'default_deployment': EC.DEPLOYMENT_DOCKER,
            'default_edge_conf_dir': _windows_config_path + '\\' + _edge_dir + '\\config',
            'default_edge_data_dir': _windows_config_path + '\\' + _edge_dir + '\\data',
            'default_edge_meta_dir_env': 'USERPROFILE',
            'deployment': {
                EC.DEPLOYMENT_DOCKER: {
                    EC.DOCKER_ENGINE_LINUX: {
                        'default_uri': 'unix:///var/run/docker.sock'
                    },
                    EC.DOCKER_ENGINE_WINDOWS: {
                        'default_uri': 'npipe://./pipe/docker_engine'
                    }
                }
            }
        },
        EC.DOCKER_HOST_DARWIN: {
            'supported_deployments': [EC.DEPLOYMENT_DOCKER],
            'default_deployment': EC.DEPLOYMENT_DOCKER,
            'default_edge_conf_dir': '/etc/' + _edge_dir,
            'default_edge_data_dir': '/var/lib/' + _edge_dir,
            'default_edge_meta_dir_env': 'HOME',
            'deployment': {
                EC.DEPLOYMENT_DOCKER: {
                    EC.DOCKER_ENGINE_LINUX: {
                        'default_uri': 'unix:///var/run/docker.sock'
                    },
                }
            }
        }
    }

    @staticmethod
    def is_host_supported(host):
        """
        Checks if the utility is supported on the given host OS.

        Args:
            host (str): Host OS should be typical values (case insensitive)
                returned by standard API platform.system().
                Ex. Windows, Linux, Darwin.

        Returns:
            True: Host is supported
            False: Otherwise

        Raises:
            edgectl.errors.EdgeInvalidArgument if host is None
        """
        if host is None:
            raise edgectl.errors.EdgeInvalidArgument('host cannot be None')

        host = host.lower()
        if host in EdgeDefault._platforms:
            return True
        return False

    @staticmethod
    def is_deployment_supported(host, deployment_type):
        """
        Checks if a deployment type is supported on the given host OS.

        Args:
            host (str): Host OS should be typical values (case insensitive)
                returned by standard API platform.system().
                Ex. Windows, Linux, Darwin.

            deployment_type (str): Currently supported deployment types is 'docker'

        Returns:
            True: Deployment is supported on the host
            False: Otherwise

        Raises:
            edgectl.errors.EdgeInvalidArgument if host or deployment_type is None
        """
        if host is None:
            raise edgectl.errors.EdgeInvalidArgument('host cannot be None')

        if deployment_type is None:
            raise edgectl.errors.EdgeInvalidArgument('deployment_type cannot be None')

        host = host.lower()
        deployment_type = deployment_type.lower()
        if host in EdgeDefault._platforms:
            if deployment_type in EdgeDefault._platforms[host]['supported_deployments']:
                return True
        return False

    @staticmethod
    def get_docker_container_types(host):
        """
        Returns a list of the supported docker container types on the
        given host OS. Ex. on Windows both Linux and Windows container
        types are supported.

        Args:
            host (str): Host OS should be typical values (case insensitive)
                returned by standard API platform.system().
                Ex. Windows, Linux, Darwin.

        Returns:
            List containing the various container types supported.
            Empty list if the host argument is unsupported

        Raises:
            edgectl.errors.EdgeInvalidArgument if host is None
        """
        if host is None:
            raise edgectl.errors.EdgeInvalidArgument('host cannot be None')

        result = []
        host = host.lower()
        if host in EdgeDefault._platforms:
            deployment = EdgeDefault._platforms[host]['deployment']
            result = list(deployment[EC.DEPLOYMENT_DOCKER].keys())
        return result

    @staticmethod
    def get_config_dir(host):
        """
        Returns the default configuration dir which contains the IoT Edge
        configuration file. Paths are host OS specific.

        Args:
            host (str): Host OS should be typical values (case insensitive)
                returned by standard API platform.system().
                Ex. Windows, Linux, Darwin.

        Returns:
            IoT Edge directory path for supported host OS.
            None otherwise.

        Raises:
            edgectl.errors.EdgeInvalidArgument if host is None
        """
        if host is None:
            raise edgectl.errors.EdgeInvalidArgument('host cannot be None')

        host = host.lower()
        if host in EdgeDefault._platforms:
            return EdgeDefault._platforms[host]['default_edge_conf_dir']
        return None

    @staticmethod
    def get_edge_ctl_diagnostic_path(host):
        """
        Returns a string detailing the environment variable path of
        of the iotedgectl utility configuration directory. Useful for
        diagnostic purposes. Paths are host OS specific.

        Args:
            host (str): Host OS should be typical values (case insensitive)
                returned by standard API platform.system().
                Ex. Windows, Linux, Darwin.

        Returns:
            iotedgectl utility configuration string for a valid host OS.
            None otherwise.

        Raises:
            edgectl.errors.EdgeInvalidArgument if host is None
        """
        if host is None:
            raise edgectl.errors.EdgeInvalidArgument('host cannot be None')

        meta_dir = None
        host = host.lower()
        if host in EdgeDefault._platforms:
            env_var = EdgeDefault._platforms[host]['default_edge_meta_dir_env']
            sep = '/'
            if host == EC.DOCKER_ENGINE_WINDOWS:
                env_var = '%%' + env_var + '%%'
                sep = '\\'
            else:
                env_var = '$' + env_var
            meta_dir = env_var + sep + EdgeDefault._edge_meta_dir_name
        return meta_dir

    @staticmethod
    def get_edge_ctl_config_dir():
        """
        Returns the directory path of the iotedgectl utility
        configuration directory for the host OS.

        Returns:
            IoT Edge utility configuration directory path string.

        Raises:
            edgectl.errors.EdgeValueError if path cannot be determined
        """
        host = platform.system().lower()
        if host not in EdgeDefault._platforms:
            msg = 'Unsupported host OS {0}'.format(host)
            log.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

        env_var = EdgeDefault._platforms[host]['default_edge_meta_dir_env']
        dir_name = os.getenv(env_var, None)
        if dir_name and dir_name.strip() != '':
            meta_dir = os.path.realpath(dir_name)
            meta_dir = os.path.join(dir_name, EdgeDefault._edge_meta_dir_name)
            return meta_dir
        else:
            msg = 'Could not find user home dir via env variable {0}'.format(env_var)
            log.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

    @staticmethod
    def get_meta_conf_file_path():
        """
        Returns the path of the iotedgectl utility configuration file.
        This file contains meta configuration to operate the the utility.
        Paths are host OS specific.

        Returns:
            IoT Edge utility configuration file path string.

        Raises:
            edgectl.errors.EdgeValueError if path cannot be determined
        """
        meta_dir = EdgeDefault.get_edge_ctl_config_dir()
        meta_conf_file = os.path.join(meta_dir, EdgeDefault._edge_meta_config_file)
        meta_conf_file = os.path.realpath(meta_conf_file)
        return meta_conf_file

    @staticmethod
    def get_config_file_name():
        """
        Returns a string containing the path of the iotedgectl utility configuration file.
        """
        return EdgeDefault._edge_config_file_name

    @staticmethod
    def get_supported_deployments(host):
        """
        Returns a list of supported deployment types supported by the given host.
        Deployments are host OS specific.

        Args:
            host (str): Host OS should be typical values (case insensitive)
                returned by standard API platform.system().
                Ex. Windows, Linux, Darwin.

        Returns:
            A list of supported deployments on the host.

        Raises:
            edgectl.errors.EdgeInvalidArgument if host is None
        """
        if host is None:
            raise edgectl.errors.EdgeInvalidArgument('host cannot be None')

        host = host.lower()
        if host in EdgeDefault._platforms:
            return EdgeDefault._platforms[host]['supported_deployments']
        return []

    @staticmethod
    def get_certificate_subject_dict():
        """
        Returns a dictionary containing default values for the X.509
        certificate subject fields

        Returns:
            A dictionary with the following strings as keys values.
            Here are the keys:
            EdgeConstants.SUBJECT_COUNTRY_KEY
            EdgeConstants.SUBJECT_STATE_KEY
            EdgeConstants.SUBJECT_LOCALITY_KEY
            EdgeConstants.SUBJECT_ORGANIZATION_KEY
            EdgeConstants.SUBJECT_ORGANIZATION_UNIT_KEY
            EdgeConstants.SUBJECT_COMMON_NAME_KEY
        """
        return EdgeDefault._cert_default_dict

    @staticmethod
    def get_docker_uri(host, container_type):
        """
        Returns a docker URI string given a host and underlying docker
        container_type.

        Args:
            host (str): Host OS should be typical values (case insensitive)
                returned by standard API platform.system().
                Ex. Windows, Linux, Darwin.

            container_type (str): Container type should be typical
                values (case insensitive) returned by standard API platform.system().
                Ex. Windows, Linux.

        Returns:
            Docker URI string for a valid host OS and container type.
            None otherwise.

        Raises:
            edgectl.errors.EdgeInvalidArgument if host or container type is None
        """
        if host is None:
            raise edgectl.errors.EdgeInvalidArgument('host cannot be None')

        if container_type is None:
            raise edgectl.errors.EdgeInvalidArgument('container_type cannot be None')

        uri = None
        host = host.lower()
        container_type = container_type.lower()
        if host in EdgeDefault._platforms:
            deployment = EdgeDefault._platforms[host]['deployment'][EC.DEPLOYMENT_DOCKER]
            if container_type in deployment:
                uri = deployment[container_type]['default_uri']
        return uri

    @staticmethod
    def get_home_dir(host):
        """
        Returns the default IoT Edge home directory for a given a host.

        Args:
            host (str): Host OS should be typical values (case insensitive)
                returned by standard API platform.system().
                Ex. Windows, Linux, Darwin.

        Returns:
            The default IoT Edge home directory for a valid host as a string
            None otherwise.

        Raises:
            edgectl.errors.EdgeInvalidArgument if host is None
        """
        if host is None:
            raise edgectl.errors.EdgeInvalidArgument('host cannot be None')

        path = None
        host = host.lower()
        if host in EdgeDefault._platforms:
            path = EdgeDefault._platforms[host]['default_edge_data_dir']
        return path

    @staticmethod
    def get_default_settings_file_path():
        """
        Returns the path of a configuration file containing default settings
        for an IoT Edge deployment. This file is installed along with the
        installation of the iotedgectl utility. These default settings will
        be used when users do not specify all inputs to setup the Edge.

        Returns:
            The default settings configuration file as a string.
        """
        script_dir_path = os.path.dirname(os.path.realpath(__file__))
        path = os.path.join(script_dir_path, EdgeDefault._edge_ref_config_file)
        return os.path.realpath(path)

    @staticmethod
    def get_default_settings_json():
        """
        Returns a dictionary respresentation of the JSON configuration file
        returned by EdgeDefault.get_default_settings_file_path()

        Returns:
            The default settings configuration file as a dictionary.

        Raises:
            edgectl.errors.EdgeFileAccessError if the file could not be read
            edgectl.errors.EdgeFileParseError if the JSON file could not be parsed
        """
        try:
            config_file = EdgeDefault.get_default_settings_file_path()
            with open(config_file, 'r') as input_file:
                data = json.load(input_file)
                return data
        except IOError as ex_os:
            log.error('Error reading defaults config file: %s. Errno: %s, Error %s',
                      config_file, str(ex_os.errno), ex_os.strerror)
            raise edgectl.errors.EdgeFileAccessError('Cannot read file', config_file)
        except ValueError as ex_value:
            log.error('Could not parse %s. JSON Parser Error: %s', config_file, str(ex_value))
            log.critical('Edge defaults config file %s is invalid.', config_file)
            raise edgectl.errors.EdgeFileParseError('Error parsing file', config_file)

    @staticmethod
    def get_runtime_log_levels():
        """
        Returns a list of the various log levels of the IoT Edge runtime.
        """
        return EdgeDefault._edge_runtime_log_levels

    @staticmethod
    def get_default_runtime_log_level():
        """
        Returns the default log level of the IoT Edge runtime.
        """
        return EC.EDGE_RUNTIME_LOG_LEVEL_INFO
