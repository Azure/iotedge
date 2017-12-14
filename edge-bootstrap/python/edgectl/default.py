import logging as log
import json
import os
import platform
from edgectl.dockerclient import EdgeDockerClient
import edgectl.errors
import edgectl.edgeconstants as EC

class EdgeDefault(object):
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
        EC.SUBJECT_COMMON_NAME_KEY: 'Edge Device CA',
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
                        'default_uri': 'unix:///var/run/docker.sock',
                        'default_module_cert_dir': '/var/run/azure-iot-edge/certs'
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
                        'default_uri': 'unix:///var/run/docker.sock',
                        'default_module_cert_dir': '/var/run/azure-iot-edge/certs'
                    },
                    EC.DOCKER_ENGINE_WINDOWS: {
                        'default_uri': 'npipe://./pipe/docker_engine',
                        'default_module_cert_dir': 'c:\\azure-iot-edge\\certs'
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
                        'default_uri': 'unix:///var/run/docker.sock',
                        'default_module_cert_dir': '/var/run/azure-iot-edge/certs'
                    },
                }
            }
        }
    }

    @staticmethod
    def is_platform_supported():
        host = platform.system().lower()
        if host not in EdgeDefault._platforms:
            log.error('Unsupported host platform: %s', host)
            return False
        return True

    @staticmethod
    def is_deployment_supported(deployment_type):
        result = False
        log.debug('Checking Edge dependencies for deployment: %s', deployment_type)
        host = platform.system().lower()
        if deployment_type not in EdgeDefault._platforms[host]['supported_deployments']:
            log.error('Unsupported Edge deployment mechanism: %s', deployment_type)
        else:
            if deployment_type == EC.DEPLOYMENT_DOCKER:
                engines = list(EdgeDefault._platforms[host]['deployment'][deployment_type].keys())
                client = EdgeDockerClient()
                if client.check_availability() is False:
                    log.error('Docker is unavailable')
                else:
                    try:
                        engine_os = client.get_os_type()
                        if engine_os.lower() not in engines:
                            log.error('Unsupported docker OS type: %s', engine_os)
                        else:
                            result = True
                    except edgectl.errors.EdgeDeploymentError as edge_err:
                        log.error('Docker get OS type returned errors')
                        print(edge_err)
                        result = False
        return result

    @staticmethod
    def get_agent_dir_name():
        return EdgeDefault._edge_agent_dir_name

    @staticmethod
    def get_config_dir(host):
        return EdgeDefault._platforms[host]['default_edge_conf_dir']

    @staticmethod
    def get_meta_conf_file_path_help_menu(host):
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
    def get_meta_conf_dir(host):
        env_var = EdgeDefault._platforms[host]['default_edge_meta_dir_env']
        dir_name = os.getenv(env_var, None)
        if dir_name and dir_name.strip() != '':
            meta_dir = os.path.realpath(dir_name)
            meta_dir = os.path.join(dir_name, EdgeDefault._edge_meta_dir_name)
        else:
            msg = 'Could not find user home dir via env variable {0}'.format(env_var)
            log.error(msg)
            raise edgectl.errors.EdgeValueError(msg)
        return meta_dir

    @staticmethod
    def get_host_meta_conf_dir():
        host = platform.system().lower()
        return EdgeDefault.get_meta_conf_dir(host)

    @staticmethod
    def get_meta_conf_file_path(host):
        meta_dir = EdgeDefault.get_meta_conf_dir(host)
        meta_conf_file = os.path.join(meta_dir, EdgeDefault._edge_meta_config_file)
        return os.path.realpath(meta_conf_file)

    @staticmethod
    def get_host_meta_conf_file_path():
        host = platform.system().lower()
        return EdgeDefault.get_meta_conf_file_path(host)

    @staticmethod
    def get_host_config_dir():
        host = platform.system().lower()
        return os.path.realpath(EdgeDefault.get_config_dir(host))

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
            choice = EC.EdgeConfigDirInputSource.ENV
        elif user_input_path and user_input_path.strip() != '':
            edge_config_dir = os.path.realpath(user_input_path)
            log.info('Using user configured IoT Edge configuration dir: %s', edge_config_dir)
            choice = user_input_option
        else:
            edge_config_dir = EdgeDefault.get_host_config_dir()
            log.info('Using default IoT Edge configuration dir: %s', edge_config_dir)
            choice = EC.EdgeConfigDirInputSource.DEFAULT

        return (edge_config_dir, choice)

    @staticmethod
    def get_host_config_file_path():
        edge_conf_dir = EdgeDefault.get_host_config_dir()
        edge_conf_file_name = EdgeDefault.get_config_file_name()
        return os.path.join(edge_conf_dir, edge_conf_file_name)

    @staticmethod
    def get_config_file_name():
        return EdgeDefault._edge_config_file_name

    @staticmethod
    def get_supported_deployments():
        host = platform.system().lower()
        return EdgeDefault._platforms[host]['supported_deployments']

    @staticmethod
    def certificate_subject_dict():
        return EdgeDefault._cert_default_dict

    @staticmethod
    def docker_uri(host, engine):
        deployment = EdgeDefault._platforms[host]['deployment']
        return deployment[EC.DEPLOYMENT_DOCKER][engine]['default_uri']

    @staticmethod
    def docker_module_cert_mount_dir(engine):
        host = platform.system().lower()
        deployment = EdgeDefault._platforms[host]['deployment']
        docker_deployment = deployment[EC.DEPLOYMENT_DOCKER_KEY]
        return docker_deployment[engine]['default_module_cert_dir']

    @staticmethod
    def get_platform_docker_uri():
        plat = platform.system().lower()
        dc = EdgeDockerClient()
        engine_os = dc.get_os_type()
        return EdgeDefault.docker_uri(plat, engine_os)

    @staticmethod
    def get_home_dir(host):
        path = None
        if EdgeDefault._platforms[host]:
            path = EdgeDefault._platforms[host]['default_edge_data_dir']
        return path

    @staticmethod
    def get_platform_home_dir():
        plat = platform.system().lower()
        return os.path.realpath(EdgeDefault.get_home_dir(plat))

    @staticmethod
    def default_user_input_config_relative_file_path():
        script_dir_path = os.path.dirname(os.path.realpath(__file__))
        path = os.path.join(script_dir_path, 'config',
                            EdgeDefault._edge_ref_config_file)
        if os.path.exists(path):
            return os.path.join('.', 'config',
                                EdgeDefault._edge_ref_config_file)
        raise edgectl.errors.EdgeFileAccessError('Default config file not found.', path)

    @staticmethod
    def default_user_input_config_abs_file_path():
        script_dir_path = os.path.dirname(os.path.realpath(__file__))
        path = os.path.join(script_dir_path, 'config',
                            EdgeDefault._edge_ref_config_file)
        return os.path.realpath(path)

    @staticmethod
    def get_default_user_input_config_json():
        try:
            config_file = EdgeDefault.default_user_input_config_abs_file_path()
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
    def edge_runtime_log_levels():
        return EdgeDefault._edge_runtime_log_levels

    @staticmethod
    def edge_runtime_default_log_level():
        return EC.EDGE_RUNTIME_LOG_LEVEL_INFO
