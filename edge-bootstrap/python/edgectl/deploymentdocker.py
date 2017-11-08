from __future__ import print_function
import logging as log
import platform
import docker
import edgectl.edgeconstants as EC
from edgectl.dockerclient import EdgeDockerClient
from edgectl.default import EdgeDefault
from edgectl.commandbase import EdgeDeploymentCommand
from edgectl.edgehostplatform import EdgeHostPlatform

class EdgeDeploymentCommandDocker(EdgeDeploymentCommand):
    _edge_runtime_container_name = 'edgeAgent'
    _edge_runtime_network_name = 'azure-iot-edge'
    _edge_agent_container_label = \
            'net.azure-devices.edge.owner=Microsoft.Azure.Devices.Edge.Agent'
    _EDGE_VOL_MOUNT_BASE = 'mnt'
    _EDGE_HUB_VOL_NAME = 'edgehub'
    _EDGE_MODULE_VOL_NAME = 'edgemodule'

    def __init__(self, config_obj):
        EdgeDeploymentCommand.__init__(self, config_obj)
        self._client = EdgeDockerClient()
        return

    def _obtain_edge_agent_login(self):
        result = None
        edge_reg = self._config_obj.deployment_config.edge_image_repository
        for registry in self._config_obj.deployment_config.registries:
            if registry['address'] == edge_reg:
                result = registry
                break
        return result

    def _recreate_agent_container(self):
        container_name = self._edge_runtime_container_name
        status = self.status()
        if status == self.EDGE_RUNTIME_STATUS_RESTARTING:
            log.error('Runtime is restarting. Please retry later.')
        elif status == self.EDGE_RUNTIME_STATUS_STOPPED:
            log.info('Runtime container %s found in stopped state. Please use' \
                     ' the start command to see changes take effect.',
                     container_name)
        elif status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
            log.info('Please use the start command to see changes take effect.')
        else:
            log.info('Stopping runtime.')
            self._client.stop(container_name)
            self._client.remove(container_name)
            log.info('Stopped runtime.')
            log.info('Starting runtime.')
            self.start()
            log.info('Starting runtime.')

    def _mount_certificates_into_agent_container(self):
        os_type = self._client.get_os_type().lower()
        if os_type != 'linux':
            return

        container_name = self._edge_runtime_container_name

        sep = '/'
        mnt_path = '{0}{1}'.format(sep, self._EDGE_VOL_MOUNT_BASE)
        # setup module volume with CA cert
        ca_cert_file = EdgeHostPlatform.get_ca_cert_file()
        module_vol_path = '{0}{1}{2}'.format(mnt_path, sep, self._EDGE_MODULE_VOL_NAME)
        self._client.copy_file_to_volume(container_name,
                                         ca_cert_file['file_name'],
                                         module_vol_path,
                                         ca_cert_file['file_path'])

        # setup hub volume CA chain and Edge server certs
        ca_chain_cert_file = EdgeHostPlatform.get_ca_chain_cert_file()
        hub_cert_dict = EdgeHostPlatform.get_hub_cert_pfx_file()
        hub_vol_path = '{0}{1}{2}'.format(mnt_path, sep, self._EDGE_HUB_VOL_NAME)
        self._client.copy_file_to_volume(container_name,
                                         ca_chain_cert_file['file_name'],
                                         hub_vol_path,
                                         ca_chain_cert_file['file_path'])
        self._client.copy_file_to_volume(container_name,
                                         hub_cert_dict['file_name'],
                                         hub_vol_path,
                                         hub_cert_dict['file_path'])


    def _setup_certificates(self, env_dict, volume_dict):
        os_type = self._client.get_os_type().lower()
        if os_type != 'linux':
            return
        # create volumes for mounting certs into hub and all other edge modules
        self._client.create_volume(self._EDGE_HUB_VOL_NAME)
        self._client.create_volume(self._EDGE_MODULE_VOL_NAME)

        sep = '/'
        mnt_path = '{0}{1}'.format(sep, self._EDGE_VOL_MOUNT_BASE)
        # add volume mounts into edge agent
        hub_vol_path = '{0}{1}{2}'.format(mnt_path, sep, self._EDGE_HUB_VOL_NAME)
        volume_dict[self._EDGE_HUB_VOL_NAME] = {'bind': hub_vol_path, 'mode': 'rw'}
        module_vol_path = \
            '{0}{1}{2}'.format(mnt_path, sep, self._EDGE_MODULE_VOL_NAME)
        volume_dict[self._EDGE_MODULE_VOL_NAME] = {'bind': module_vol_path, 'mode': 'rw'}

        # setup env vars describing volume names and paths
        env_dict['EdgeHubVolumeName'] = self._EDGE_HUB_VOL_NAME
        env_dict['EdgeHubVolumePath'] = hub_vol_path
        env_dict['EdgeModuleVolumeName'] = self._EDGE_MODULE_VOL_NAME
        env_dict['EdgeModuleVolumePath'] = module_vol_path

        # setup env vars describing CA cert location for all edge modules
        ca_cert_file = EdgeHostPlatform.get_ca_cert_file()
        env_dict['EdgeModuleCACertificateFile'] = \
            '{0}{1}{2}'.format(module_vol_path, sep, ca_cert_file['file_name'])

        # setup env vars describing CA cert location for all edge hub
        ca_chain_cert_file = EdgeHostPlatform.get_ca_chain_cert_file()
        env_dict['EdgeModuleHubServerCAChainCertificateFile'] = \
            '{0}{1}{2}'.format(hub_vol_path, sep, ca_chain_cert_file['file_name'])
        hub_cert_dict = EdgeHostPlatform.get_hub_cert_pfx_file()
        env_dict['EdgeModuleHubServerCertificateFile'] = \
            '{0}{1}{2}'.format(hub_vol_path, sep, hub_cert_dict['file_name'])

    def _setup_registries(self, env_dict):
        edge_config = self._config_obj
        idx = 0
        for registry in edge_config.deployment_config.registries:
            key = 'DockerRegistryAuth__' + str(idx) + '__serverAddress'
            val = registry['address']
            env_dict[key] = val

            key = 'DockerRegistryAuth__' + str(idx) + '__username'
            val = registry['username']
            env_dict[key] = val

            key = 'DockerRegistryAuth__' + str(idx) + '__password'
            val = registry['password']
            env_dict[key] = val
            idx = idx + 1

    def _setup_docker_logging(self, env_dict, log_config_dict):
        edge_config = self._config_obj
        # set the log driver type
        log_driver = edge_config.deployment_config.logging_driver
        env_dict['DockerLoggingDriver'] = log_driver
        log_config_dict['type'] = log_driver

        # set the log driver option kvp
        log_opts_dict = edge_config.deployment_config.logging_options
        log_config_dict['config'] = log_opts_dict
        opts = list(log_opts_dict.items())
        for key, val in opts:
            key = 'DockerLoggingOptions__' + key
            env_dict[key] = val

    def _setup_docker_uri_endpoint(self, ports_dict, volume_dict, mounts_list):
        edge_config = self._config_obj
        # if the uri has a port, create a port mapping else
        # volume mount the end point (ex. unix socket file)
        if edge_config.deployment_config.uri_port and \
                edge_config.deployment_config.uri_port != '':
            key = edge_config.deployment_config.uri_port + '/tcp'
            val = int(edge_config.deployment_config.uri_port)
            ports_dict[key] = val
        else:
            if self._client.get_os_type() == 'windows':
                # Windows needs 'mounts' to mount named pipe to Agent
                docker_pipe = edge_config.deployment_config.uri_endpoint
                mounts_list.append(docker.types.Mount(target=docker_pipe,
                                                      source=docker_pipe,
                                                      type='npipe'))
            else:
                key = edge_config.deployment_config.uri_endpoint
                volume_dict[key] = {'bind': key, 'mode': 'rw'}

    def _start_agent_container(self):
        container_name = self._edge_runtime_container_name
        self._client.start(container_name)

    def _remove_agent_container(self):
        container_name = self._edge_runtime_container_name
        self._client.remove(container_name)

    def _pull_freshest_agent_image(self):
        edge_config = self._config_obj
        container_name = self._edge_runtime_container_name
        image = edge_config.deployment_config.edge_image
        edge_reg = self._obtain_edge_agent_login()
        username = None
        password = None
        if edge_reg:
            username = edge_reg['username']
            password = edge_reg['password']
        is_newer_agent_image = self._client.pull(image, username, password)
        if is_newer_agent_image:
            log.debug('Pulled new image %s', image)
        else:
            # check if user has updated the agent image by checking image names
            existing_agent_image = self._client.get_container_by_name(container_name)
            if existing_agent_image and existing_agent_image.image != image:
                is_newer_agent_image = True

        return is_newer_agent_image

    def _create_agent_container(self):
        env_dict = {}
        log_config_dict = {}
        ports_dict = {}
        volume_dict = {}
        mounts_list = []

        edge_config = self._config_obj
        # create network for running all edge modules
        nw_name = self._edge_runtime_network_name
        self._client.create_network(nw_name)

        # setup base env vars
        env_dict['DockerUri'] = edge_config.deployment_config.uri
        env_dict['DeviceConnectionString'] = edge_config.connection_string
        env_dict['EdgeDeviceHostName'] = edge_config.hostname
        env_dict['NetworkId'] = nw_name
        env_dict['RuntimeLogLevel'] = edge_config.log_level

        self._setup_certificates(env_dict, volume_dict)
        self._setup_registries(env_dict)
        self._setup_docker_logging(env_dict, log_config_dict)
        self._setup_docker_uri_endpoint(ports_dict, volume_dict, mounts_list)
        image = edge_config.deployment_config.edge_image
        container_name = self._edge_runtime_container_name
        self._client.create(image, container_name, True, env_dict, nw_name,
                            ports_dict, volume_dict, log_config_dict,
                            mounts_list)
        self._mount_certificates_into_agent_container()

    def login(self):
        log.info('Executing \'login\'')
        self._recreate_agent_container()

    def update(self):
        log.info('Executing \'update\'')
        self._recreate_agent_container()

    def start(self):
        log.info('Executing \'start\'')
        container_name = self._edge_runtime_container_name
        status = self._status()
        if status == self.EDGE_RUNTIME_STATUS_RUNNING:
            log.error('Runtime is currently running. ' \
                      'Please stop or restart the runtime and retry.')
        elif status == self.EDGE_RUNTIME_STATUS_RESTARTING:
            log.error('Runtime is currently restarting. ' \
                      'Please stop the runtime and retry.')
        else:
            # here we are either in stopped or unavailable state
            create_new_container = False

            # pull the latest edge agent image
            is_newer_agent_image = self._pull_freshest_agent_image()
            if is_newer_agent_image:
                # image was updated so remove any existing agent container
                create_new_container = True
                self._remove_agent_container()
            else:
                # image was not updated and available locally on the host
                if status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
                    # have to create a new container since one does not exist
                    create_new_container = True
                    log.debug('Edge Agent container %s does not exist.',
                              container_name)

            if create_new_container:
                self._create_agent_container()
            self._start_agent_container()
            print('Runtime started.')
        return

    def stop(self):
        log.info('Executing \'stop\'')
        container_name = self._edge_runtime_container_name

        status = self._status()
        if status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
            log.error('Edge Agent container \'%s\' does not exist.',
                      container_name)
        elif status == self.EDGE_RUNTIME_STATUS_RESTARTING:
            log.error('Runtime is restarting. Please retry later.')
        else:
            if status != self.EDGE_RUNTIME_STATUS_RUNNING:
                log.info('Runtime is already stopped.')
            else:
                self._client.stop(container_name)
            log.info('Stopping all modules.')
            self._client.stop_by_label(self._edge_agent_container_label)
            print('Runtime stopped.')
        return

    def restart(self):
        log.info('Executing \'restart\'')
        container_name = self._edge_runtime_container_name

        status = self._status()
        if status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
            self.start()
        elif status == self.EDGE_RUNTIME_STATUS_RESTARTING:
            log.error('Runtime is restarting. Please retry later.')
        else:
            self._client.restart(container_name)
            print('Runtime restarted.')
        return

    def status(self):
        result = self._status()
        print('IoT Edge Status: {0}'.format(result))
        return result

    def _status(self):
        result = EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        status = self._client.status(self._edge_runtime_container_name)
        if status is not None:
            if status == 'running':
                result = self.EDGE_RUNTIME_STATUS_RUNNING
            elif status == 'restarting':
                result = self.EDGE_RUNTIME_STATUS_RESTARTING
            else:
                result = self.EDGE_RUNTIME_STATUS_STOPPED
        return result

    def uninstall_common(self):
        container_name = self._edge_runtime_container_name

        status = self._status()
        log.info('Uninstalling all modules.')
        if status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
            log.debug('Edge Agent container \'%s\' does not exist.',
                      container_name)
        else:
            self._client.stop(container_name)
            self._client.remove(container_name)

        self._client.stop_by_label(self._edge_agent_container_label)
        self._client.remove_by_label(self._edge_agent_container_label)
        # create volumes for mounting certs into hub and all other edge modules
        self._client.remove_volume(self._EDGE_HUB_VOL_NAME, True)
        self._client.remove_volume(self._EDGE_MODULE_VOL_NAME, True)
        return

    def uninstall(self):
        log.info('Executing \'uninstall\'')
        self.uninstall_common()
        print('Runtime uninstalled successfully.')
        return

    def setup(self):
        log.info('Executing \'setup\'')
        self.uninstall_common()
        print('Runtime setup successfully.')
        print('\n')
        print('Using configration:\n\n%s' %(self._config_obj,))
        print('Use \'iotedgectl start\' to start the runtime.')
        return
