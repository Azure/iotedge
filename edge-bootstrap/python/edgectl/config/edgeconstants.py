from enum import Enum

class EdgeConstants(object):
    # JSON Edge Config Constants
    SCHEMA_KEY = 'schemaVersion'
    DEVICE_CONNECTION_STRING_KEY = 'deviceConnectionString'
    CONFIG_DIR_KEY = 'configDir'
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
    PREINSTALLED_KEY = 'preInstalled'
    PREINSTALLED_OWNER_CA_CERT_FILE_KEY = 'ownerCACertificateFilePath'
    PREINSTALLED_DEVICE_CA_CERT_FILE_KEY = 'deviceCACertificateFilePath'
    PREINSTALLED_DEVICE_CA_CHAIN_CERT_FILE_KEY = 'deviceCAChainCertificateFilePath'
    PREINSTALLED_DEVICE_CA_PRIVATE_KEY_FILE_KEY = 'deviceCAPrivateKeyFilePath'
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

    ENV_EDGECONFIGDIR = 'EDGECONFIGDIR'

    # Edge Log Level Log Constants
    EDGE_RUNTIME_LOG_LEVEL_INFO = 'info'
    EDGE_RUNTIME_LOG_LEVEL_DEBUG = 'debug'

# Edge input configuration sources
class EdgeConfigInputSources(Enum):
    FILE = 1
    CLI = 2

# Edge config file directory input sources
class EdgeConfigDirInputSource(Enum):
    ENV = 'env'
    USER_PROVIDED = 'userProvided'
    DEFAULT = 'default'
    NONE = ''
