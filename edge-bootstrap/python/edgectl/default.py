import logging as log
import json
import os
import platform
from edgectl.dockerclient import EdgeDockerClient

import edgectl.edgeconstants as EC

class EdgeDefault(object):
    _edge_dir = 'azure-iot-edge'
    _edge_config_file = 'config.json'
    _edge_ref_config_file = 'azure-iot-edge-config-reference.json'
    _edge_agent_dir_name = "__AzureIoTEdgeAgent"
    _edge_runtime_log_levels = [EC.EDGE_RUNTIME_LOG_LEVEL_INFO,
                                EC.EDGE_RUNTIME_LOG_LEVEL_DEBUG]

    _platforms = {
        EC.DOCKER_HOST_LINUX: {
            'supported_deployments': [EC.DEPLOYMENT_DOCKER],
            'default_deployment': EC.DEPLOYMENT_DOCKER,
            'default_edge_conf_dir': '/etc/' + _edge_dir,
            'default_edge_data_dir': '/var/lib/' + _edge_dir,
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
            'default_edge_conf_dir': 'C:\\ProgramData\\' + _edge_dir,
            'default_edge_data_dir': 'C:\\' + _edge_dir,
            'deployment': {
                'docker': {
                    EC.DOCKER_ENGINE_LINUX: {
                        'default_uri': 'unix:///var/run/docker.sock',
                        'default_module_cert_dir': '/var/run/azure-iot-edge/certs'
                    },
                    EC.DOCKER_ENGINE_WINDOWS: {
                        'default_uri': 'npipe:////./pipe/docker_engine',
                        'default_module_cert_dir': '\temp\azure-iot-edge\certs'
                    }
                }
            }
        },
        # @todo darwin
    }
    @staticmethod
    def is_platform_supported():
        host = platform.system().lower()
        if host in EdgeDefault._platforms:
            return True
        return False

    @staticmethod
    def get_agent_dir_name():
        return EdgeDefault._edge_agent_dir_name

    @staticmethod
    def get_host_config_dir():
        host = platform.system().lower()
        result = EdgeDefault._platforms[host]['default_edge_conf_dir']
        return result

    @staticmethod
    def get_host_config_file_path():
        edge_conf_dir = EdgeDefault.get_host_config_dir()
        result = os.path.join(edge_conf_dir, EdgeDefault._edge_config_file)
        return result

    @staticmethod
    def get_supported_deployments():
        host = platform.system().lower()
        return EdgeDefault._platforms[host]['supported_deployments']

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
        if EdgeDefault._platforms[host]:
            path = EdgeDefault._platforms[host]['default_edge_data_dir']
            return path
        else:
            raise RuntimeError('Unsupported Host Platform ' + host)

    @staticmethod
    def get_platform_home_dir():
        plat = platform.system().lower()
        return EdgeDefault.get_home_dir(plat)

    @staticmethod
    def default_user_input_config_relative_file_path():
        script_dir_path = os.path.dirname(os.path.realpath(__file__))
        path = os.path.join(script_dir_path, 'config',
                            EdgeDefault._edge_ref_config_file)
        if os.path.exists(path):
            return os.path.join('.', 'config',
                                EdgeDefault._edge_ref_config_file)
        raise ValueError('Default Config File Not Found:' + path)

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
        except IOError as ex:
            log.error('Error Observed When Reading Default Config File: ' \
                      + config_file + '. Errno ' + str(ex.errno) \
                      + ', Error:' + ex.strerror)
            raise

    @staticmethod
    def edge_runtime_log_levels():
        return EdgeDefault._edge_runtime_log_levels

    @staticmethod
    def edge_runtime_default_log_level():
        return EC.EDGE_RUNTIME_LOG_LEVEL_INFO
