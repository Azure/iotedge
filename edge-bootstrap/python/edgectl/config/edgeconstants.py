"""
    This module defines various constants which could be either data or configuration.
    These are the classes implemented:
        EdgeConstants
        EdgeConfigInputSources
        EdgeConfigDirInputSource
"""
import collections
from enum import Enum

EdgeConstantsTuple = collections.namedtuple('EdgeConstantsTuple',
                                            ['SCHEMA_KEY',
                                             'DEVICE_CONNECTION_STRING_KEY',
                                             'CONFIG_DIR_KEY',
                                             'CONFIG_DIR_SOURCE_KEY',
                                             'HOMEDIR_KEY',
                                             'HOSTNAME_KEY',
                                             'EDGE_RUNTIME_LOG_LEVEL_KEY',
                                             'SECURITY_KEY',
                                             'CERTS_KEY',
                                             'CERTS_OPTION_KEY',
                                             'FORCENOPASSWD_KEY',
                                             'DEVICE_CA_PASSPHRASE_FILE_KEY',
                                             'AGENT_CA_PASSPHRASE_FILE_KEY',
                                             'SELFSIGNED_KEY',
                                             'PREINSTALL_KEY',
                                             'PREINSTALL_OWNER_CA_CERT_KEY',
                                             'PREINSTALL_DEVICE_CERT_KEY',
                                             'PREINSTALL_DEVICE_CHAINCERT_KEY',
                                             'PREINSTALL_DEVICE_PRIVKEY_KEY',
                                             'CERTS_SUBJECT_KEY',
                                             'SUBJECT_COUNTRY_KEY',
                                             'SUBJECT_STATE_KEY',
                                             'SUBJECT_LOCALITY_KEY',
                                             'SUBJECT_ORGANIZATION_KEY',
                                             'SUBJECT_ORGANIZATION_UNIT_KEY',
                                             'SUBJECT_COMMON_NAME_KEY',
                                             'DEPLOYMENT_KEY',
                                             'DEPLOYMENT_TYPE_KEY',
                                             'DEPLOYMENT_DOCKER_KEY',
                                             'EDGE_RUNTIME_IMAGE_KEY',
                                             'REGISTRIES_KEY',
                                             'REGISTRY_ADDRESS_KEY',
                                             'REGISTRY_USERNAME_KEY',
                                             'REGISTRY_PASSWORD_KEY',
                                             'DOCKER_URI_KEY',
                                             'DOCKER_LOGGING_OPTS_KEY',
                                             'DOCKER_LOGGING_DRIVER_KEY',
                                             'DOCKER_LOGGING_DRIVER_OPTS_KEY'
                                             'DEPLOYMENT_DOCKER',
                                             'DOCKER_HOST_LINUX',
                                             'DOCKER_HOST_WINDOWS',
                                             'DOCKER_HOST_DARWIN',
                                             'DOCKER_ENGINE_LINUX',
                                             'DOCKER_ENGINE_WINDOWS',
                                             'DOCKER_ENGINE_WINDOWS_ENDPOINT',
                                             'ENV_EDGECONFIGDIR',
                                             'EDGE_RUNTIME_LOG_LEVEL_INFO',
                                             'EDGE_RUNTIME_LOG_LEVEL_DEBUG'])

class EdgeConstants(EdgeConstantsTuple):
    """
        This class defines various keys and values used to setup
        and operate the IoT Edge runtime
    """
    # JSON Edge Config Constants
    SCHEMA_KEY = 'schemaVersion'
    DEVICE_CONNECTION_STRING_KEY = 'deviceConnectionString'
    CONFIG_DIR_KEY = 'configDir'
    CONFIG_DIR_SOURCE_KEY = 'configDirSource'
    HOMEDIR_KEY = 'homeDir'
    HOSTNAME_KEY = 'hostName'
    EDGE_RUNTIME_LOG_LEVEL_KEY = 'logLevel'
    SECURITY_KEY = 'security'
    CERTS_KEY = 'certificates'
    CERTS_OPTION_KEY = 'option'
    FORCENOPASSWD_KEY = 'forceNoPasswords'
    DEVICE_CA_PASSPHRASE_FILE_KEY = 'deviceCAPassphraseFilePath'
    AGENT_CA_PASSPHRASE_FILE_KEY = 'agentCAPassphraseFilePath'
    SELFSIGNED_KEY = 'selfSigned'
    PREINSTALL_KEY = 'preInstalled'
    PREINSTALL_OWNER_CA_CERT_KEY = 'ownerCACertificateFilePath'
    PREINSTALL_DEVICE_CERT_KEY = 'deviceCACertificateFilePath'
    PREINSTALL_DEVICE_CHAINCERT_KEY = 'deviceCAChainCertificateFilePath'
    PREINSTALL_DEVICE_PRIVKEY_KEY = 'deviceCAPrivateKeyFilePath'
    CERTS_SUBJECT_KEY = 'subject'
    SUBJECT_COUNTRY_KEY = 'countryCode'
    SUBJECT_STATE_KEY = 'state'
    SUBJECT_LOCALITY_KEY = 'locality'
    SUBJECT_ORGANIZATION_KEY = 'organization'
    SUBJECT_ORGANIZATION_UNIT_KEY = 'organizationUnit'
    SUBJECT_COMMON_NAME_KEY = 'commonName'
    DEPLOYMENT_KEY = 'deployment'
    DEPLOYMENT_TYPE_KEY = 'type'
    DEPLOYMENT_DOCKER_KEY = 'docker'
    EDGE_RUNTIME_IMAGE_KEY = 'edgeRuntimeImage'
    REGISTRIES_KEY = 'registries'
    REGISTRY_ADDRESS_KEY = 'address'
    REGISTRY_USERNAME_KEY = 'username'
    REGISTRY_PASSWORD_KEY = 'password'
    DOCKER_URI_KEY = 'uri'
    DOCKER_LOGGING_OPTS_KEY = 'loggingOptions'
    DOCKER_LOGGING_DRIVER_KEY = 'log-driver'
    DOCKER_LOGGING_DRIVER_OPTS_KEY = 'log-opts'
    UPSTREAM_PROTOCOL = 'upstreamProtocol'

    # Docker Constants
    DEPLOYMENT_DOCKER = DEPLOYMENT_DOCKER_KEY
    DOCKER_HOST_LINUX = 'linux'
    DOCKER_HOST_WINDOWS = 'windows'
    DOCKER_HOST_DARWIN = 'darwin'
    DOCKER_ENGINE_LINUX = 'linux'
    DOCKER_ENGINE_WINDOWS = 'windows'
    DOCKER_ENGINE_WINDOWS_ENDPOINT = '\\\\.\\pipe\\docker_engine'

    ENV_EDGECONFIGDIR = 'EDGECONFIGDIR'

    # Edge Log Level Log Constants
    EDGE_RUNTIME_LOG_LEVEL_INFO = 'info'
    EDGE_RUNTIME_LOG_LEVEL_DEBUG = 'debug'


# pylint: disable=R0903
# suppress the Too few public methods lint warning for enums
class EdgeConfigInputSources(Enum):
    """ Enum to define the various input sources to setup the IoT Edge runtime"""
    FILE = 1
    CLI = 2


# pylint: disable=R0903
# suppress the Too few public methods lint warning for enums
class EdgeConfigDirInputSource(Enum):
    """ Enum to define the various mechanisms to setup the IoT Edge
        runtime configuration directory
    """
    ENV = 'env'
    USER_PROVIDED = 'userProvided'
    DEFAULT = 'default'
    NONE = ''

# pylint: disable=R0903
# suppress the Too few public methods lint warning for enums
class EdgeUpstreamProtocol(Enum):
    """ Enum to define the protocol used by IoT Edge runtime
        to communicate to IoT Hub upstream
    """
    AMQP = 'Amqp'
    AMQPWS = 'AmqpWs'
    MQTT = 'Mqtt'
    MQTTWS = 'MqttWs'
    NONE = ''