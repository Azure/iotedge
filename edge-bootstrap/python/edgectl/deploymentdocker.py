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

    def __init__(self, config_obj):
        EdgeDeploymentCommand.__init__(self, config_obj)
        self._client = EdgeDockerClient()
        return

    def obtain_edge_agent_login(self):
        result = None
        edge_reg = self._config_obj.deployment_config.edge_image_repository
        for registry in self._config_obj.deployment_config.registries:
            if registry['address'] == edge_reg:
                result = registry
                break
        return result

    def login(self):
        log.info('Executing \'login\'')
        container_name = self._edge_runtime_container_name

        status = self.status()
        if status == self.EDGE_RUNTIME_STATUS_RESTARTING:
            log.error('Runtime is restarting. Please retry later.')
        elif status == self.EDGE_RUNTIME_STATUS_STOPPED:
            log.info('Runtime container ' + container_name \
                     + ' found in stopped state. Please use' \
                     + ' the start command to see changes take effect.')
        elif status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
            log.info('Please use the start command to see' \
                     + ' changes take effect.')
        else:
            log.info('Stopping runtime.')
            self._client.stop(container_name)
            self._client.remove(container_name)
            log.info('Stopped runtime.')
            log.info('Starting runtime.')
            self.start()
            log.info('Starting runtime.')

    def update(self):
        log.info('Executing \'update\'')

        container_name = self._edge_runtime_container_name

        status = self.status()
        if status == self.EDGE_RUNTIME_STATUS_RESTARTING:
            log.error('Runtime is restarting. Please retry later.')
        elif status == self.EDGE_RUNTIME_STATUS_STOPPED:
            log.info('Runtime container ' + container_name \
                     + ' found in stopped state. Please use' \
                     + ' the start command to see changes take effect.')
        elif status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
            log.info('Please use the start command to see' \
                     + ' changes take effect.')
        else:
            log.info('Stopping runtime.')
            self._client.stop(container_name)
            self._client.remove(container_name)
            log.info('Stopped runtime.')
            log.info('Starting runtime.')
            self.start()
            log.info('Starting runtime.')

    def start(self):
        log.info('Executing \'start\'')
        create_new_container = False
        start_existing_container = False
        container_name = self._edge_runtime_container_name
        edge_config = self._config_obj

        image = edge_config.deployment_config.edge_image
        status = self._status()
        if status == self.EDGE_RUNTIME_STATUS_RUNNING:
            log.error('Runtime is currently running. '
                      + 'Please stop or restart the runtime and retry.')
        elif status == self.EDGE_RUNTIME_STATUS_RESTARTING:
            log.error('Runtime is currently restarting. '
                      + 'Please stop the runtime and retry.')
        else:
            edge_reg = self.obtain_edge_agent_login()
            username = None
            password = None
            if edge_reg:
                username = edge_reg['username']
                password = edge_reg['password']
            is_updated = self._client.pull(image, username, password)
            if is_updated:
                log.debug('Pulled new image ' + image)
                create_new_container = True
                self._client.remove(container_name)
            else:
                if status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
                    create_new_container = True
                    log.debug('Edge Agent container ' + container_name
                              + ' does not exist.')
                else:
                    existing = self._client.get_container_by_name(container_name)
                    if existing.image != image:
                        log.debug('Did not pull new image and container exists with image ' + str(existing.image))
                        create_new_container = True
                        self._client.remove(container_name)
                    else:
                        start_existing_container = True

        if start_existing_container:
            self._client.start(container_name)
            print('Runtime started.')
        elif create_new_container:
            ca_cert_file = EdgeHostPlatform.get_ca_cert_file()
            if ca_cert_file is None:
                raise RuntimeError('Could not find CA certificate file')

            ca_chain_cert_file = EdgeHostPlatform.get_ca_chain_cert_file()
            if ca_chain_cert_file is None:
                raise RuntimeError('Could not find CA chain certificate file')

            hub_cert_dict = EdgeHostPlatform.get_hub_cert_pfx_file()
            if hub_cert_dict is None:
                raise RuntimeError('Could not find Edge Hub certificate.')

            nw_name = self._edge_runtime_network_name
            self._client.create_network(nw_name)

            os_type = self._client.get_os_type()
            os_type = os_type.lower()
            module_certs_path = \
                EdgeDefault.docker_module_cert_mount_dir(os_type)
            if os_type == 'windows':
                sep = '\\'
            else:
                sep = '/'
            module_certs_path += sep
            env_dict = {
                'DockerUri': edge_config.deployment_config.uri,
                'DeviceConnectionString': edge_config.connection_string,
                'EdgeDeviceHostName': edge_config.hostname,
                'NetworkId': nw_name,
            }
            # @todo disable mounting certs for non Linux hosts
            host = platform.system().lower()
            if host == 'linux':
                env_dict['EdgeHostCACertificateFile'] = ca_cert_file['file_path']
                env_dict['EdgeModuleCACertificateFile'] = module_certs_path + ca_cert_file['file_name']
                env_dict['EdgeHostHubServerCAChainCertificateFile'] = ca_chain_cert_file['file_path']
                env_dict['EdgeModuleHubServerCAChainCertificateFile'] = module_certs_path + ca_chain_cert_file['file_name']
                env_dict['EdgeHostHubServerCertificateFile'] = hub_cert_dict['file_path']
                env_dict['EdgeModuleHubServerCertificateFile'] = module_certs_path + hub_cert_dict['file_name']

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

            # setup the Edge runtime log level
            env_dict['RuntimeLogLevel'] = edge_config.log_level

            # set the log driver type

            log_driver = edge_config.deployment_config.logging_driver
            log_config_dict = {}
            env_dict['DockerLoggingDriver'] = log_driver
            log_config_dict['type'] = log_driver

            # set the log driver option kvp
            log_opts_dict = edge_config.deployment_config.logging_options
            log_config_dict['config'] = log_opts_dict
            opts = list(log_opts_dict.items())
            for key, val in opts:
                key = 'DockerLoggingOptions__' + key
                env_dict[key] = val

            ports_dict = {}
            volume_dict = {}
            if edge_config.deployment_config.uri_port and \
                    edge_config.deployment_config.uri_port != '':
                key = edge_config.deployment_config.uri_port + '/tcp'
                val = int(edge_config.deployment_config.uri_port)
                ports_dict[key] = val
            else:
                key = edge_config.deployment_config.uri_endpoint
                volume_dict[key] = {'bind': key, 'mode': 'rw'}
            self._client.run(image,
                             container_name,
                             True,
                             env_dict,
                             nw_name,
                             ports_dict,
                             volume_dict,
                             log_config_dict)
            print('Runtime started.')
        return

    def stop(self):
        log.info('Executing \'stop\'')
        container_name = self._edge_runtime_container_name

        status = self._status()
        if status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
            log.error('Edge Agent container \'%s\' does not exist.' % (container_name,))
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
            log.info('Edge Agent container \'%s\' does not exist.' % (container_name,))
        else:
            self._client.stop(container_name)
            self._client.remove(container_name)

        self._client.stop_by_label(self._edge_agent_container_label)
        self._client.remove_by_label(self._edge_agent_container_label)
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
