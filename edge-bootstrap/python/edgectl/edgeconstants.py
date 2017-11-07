from enum import Enum

# JSON Edge Config Constants
SCHEMA_KEY = 'schemaVersion'
DEVICE_CONNECTION_STRING_KEY = 'deviceConnectionString'
HOMEDIR_KEY = 'homeDir'
HOSTNAME_KEY = 'hostName'
EDGE_RUNTIME_LOG_LEVEL_KEY = 'logLevel'
SECURITY_KEY = 'security'
CERTS_KEY = 'certificates'
CERTS_OPTION_KEY = 'option'
SELFSIGNED_KEY = 'selfSigned'
SELFSIGNED_FORCENOPASSWD_KEY = 'forceNoPasswords'
SELFSIGNED_FORCEREGEN_KEY = 'forceRegenerate'
PREINSTALLED_KEY = 'preInstalled'
PREINSTALLED_DEVICE_CA_CERT_KEY = 'deviceCACertificateFilePath'
PREINSTALLED_SERVER_CERT_KEY = 'serverCertificateFilePath'
DEPLOYMENT_KEY = 'deployment'
DEPLOYMENT_TYPE_KEY = 'type'
DEPLOYMENT_DOCKER_KEY = 'docker'
EDGE_RUNTIME_IMAGE_KEY = 'edgeRuntimeImage'
REGISTRIES_KEY = 'registries'
REGISTRY_ADDRESS_KEY = 'address'
REGISTRY_USERNAME_KEY = 'username'
REGISTRY_PASSWORD_KEY = 'password'
DOCKER_URI_KEY = 'uri'
DOCKER_LOGGING_OPTIONS_KEY = 'loggingOptions'
DOCKER_LOGGING_DRIVER_KEY = 'log-driver'
DOCKER_LOGGING_DRIVER_OPTIONS_KEY = 'log-opts'

# Docker Constants
DEPLOYMENT_DOCKER = DEPLOYMENT_DOCKER_KEY
DOCKER_HOST_LINUX = 'linux'
DOCKER_HOST_WINDOWS = 'windows'
DOCKER_HOST_DARWIN = 'darwin'
DOCKER_ENGINE_LINUX = 'linux'
DOCKER_ENGINE_WINDOWS = 'windows'

# Edge Log Level Log Constants
EDGE_RUNTIME_LOG_LEVEL_INFO = 'info'
EDGE_RUNTIME_LOG_LEVEL_DEBUG = 'debug'

# Edge input configuration sources
class EdgeConfigInputSources(Enum):
    FILE = 1
    CLI = 2
