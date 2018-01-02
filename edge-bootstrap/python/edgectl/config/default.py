import logging as log
import json
import os
import edgectl.errors
from edgectl.config.edgeconstants import EdgeConfigDirInputSource
from edgectl.config.edgeconstants import EdgeConstants as EC


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
        host = host.lower()
        if host in EdgeDefault._platforms:
            return True
        return False

    @staticmethod
    def is_deployment_supported(host, deployment_type):
        host = host.lower()
        if host in EdgeDefault._platforms:
            if deployment_type in EdgeDefault._platforms[host]['supported_deployments']:
                return True
        return False

    @staticmethod
    def get_supported_docker_engines(host):
        host = host.lower()
        if host in EdgeDefault._platforms:
            return list(EdgeDefault._platforms[host]['deployment'][EC.DEPLOYMENT_DOCKER].keys())
        return None

    @staticmethod
    def get_config_dir(host):
        host = host.lower()
        if host in EdgeDefault._platforms:
            return EdgeDefault._platforms[host]['default_edge_conf_dir']
        return None

    @staticmethod
    def get_meta_conf_file_path_help_menu(host):
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
    def get_meta_conf_dir(host):
        meta_dir = None
        host = host.lower()
        if host in EdgeDefault._platforms:
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
    def get_meta_conf_file_path(host):
        meta_conf_file = None
        meta_dir = EdgeDefault.get_meta_conf_dir(host)
        if meta_dir:
            meta_conf_file = os.path.join(meta_dir, EdgeDefault._edge_meta_config_file)
            meta_conf_file = os.path.realpath(meta_conf_file)
        return meta_conf_file

    @staticmethod
    def get_config_file_name():
        return EdgeDefault._edge_config_file_name

    @staticmethod
    def get_supported_deployments(host):
        host = host.lower()
        if host in EdgeDefault._platforms:
            return EdgeDefault._platforms[host]['supported_deployments']
        return None

    @staticmethod
    def certificate_subject_dict():
        return EdgeDefault._cert_default_dict

    @staticmethod
    def docker_uri(host, engine):
        uri = None
        host = host.lower()
        if host in EdgeDefault._platforms:
            deployment = EdgeDefault._platforms[host]['deployment']
            uri = deployment[EC.DEPLOYMENT_DOCKER][engine]['default_uri']
        return uri

    @staticmethod
    def get_home_dir(host):
        path = None
        if host in EdgeDefault._platforms:
            path = EdgeDefault._platforms[host]['default_edge_data_dir']
        return path

    @staticmethod
    def default_user_input_config_abs_file_path():
        script_dir_path = os.path.dirname(os.path.realpath(__file__))
        path = os.path.join(script_dir_path, EdgeDefault._edge_ref_config_file)
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
