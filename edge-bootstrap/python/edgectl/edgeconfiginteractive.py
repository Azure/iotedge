from __future__ import print_function
import logging as log
import os
import sys
import six

from edgectl.default import EdgeDefault
from edgectl.edgeconfig import EdgeHostConfig
from edgectl.edgeconfig import EdgeDeploymentConfigDocker
import edgectl.edgeconstants as EC
from edgectl.edgeutils import EdgeUtils

class EdgeConfigInteractive(object):
    @staticmethod
    def get_connection_string(config):
        # Get user's connection string
        done = False
        while not done:
            try:
                print('')
                msg = 'Please Enter Your Edge Device Connection String.' \
                      '\nFormat:HostName=<>;DeviceId=<>;SharedAccessKey=<>: '
                ip = six.moves.input(msg)
                config.connection_string = ip
                done = True
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                log.error(ex_val)
                print('Please try again.')

    @staticmethod
    def get_home_dir(config):
        # Get user's Edge Home Dir
        done = False
        while not done:
            try:
                default_value = EdgeDefault.get_platform_home_dir()
                print('')
                msg = 'Please Enter Your Edge Home Directory. [' \
                      + default_value + ' ]: '
                ip = six.moves.input(msg)
                if ip is None or len(ip) == 0:
                    ip = default_value
                    if os.path.isdir(ip) is False:
                        os.mkdir(ip)
                config.home_dir = ip
                done = True
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except OSError as ex_os:
                log.error('Error Observed When Creating Edge Home Dir: ' + ip \
                          + '. Errno ' + str(ex_os.errno)
                          + ', Error:' + ex_os.strerror)
            except ValueError as ex_val:
                log.error(ex_val)
                print('Please try again.')

    @staticmethod
    def get_hostname(config):
        # Get user's Edge Home Dir
        done = False
        while not done:
            try:
                default_value = EdgeUtils.get_hostname()
                print('')
                msg = 'Next, Enter Your Edge Device\'s Hostname. \n' \
                      'This value should be the FQDN when when operating the' \
                      ' Edge as a gateway where leaf devices will connect to' \
                      ' the Edge runtime.\nFor non gateway scenarios, having' \
                      ' the device be domain registered is not a requirement' \
                      ' and your machine name would be a suitable choice' \
                      '\nPlease enter your hostname ' \
                      '['+ default_value + ']: '
                ip = six.moves.input(msg)
                if ip is None or len(ip) == 0:
                    ip = default_value
                    if os.path.isdir(ip) is False:
                        os.mkdir(ip)
                config.home_dir = ip
                done = True
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                log.error(ex_val)
                print('Please try again.')

    @staticmethod
    def get_autogen_certificate_input(config):
        return

    @staticmethod
    def get_preinstalled_certificate_input(config):
        done = False
        while not done:
            try:
                print('')
                msg = 'Please Enter Path to the Device CA Certificate: '
                ip = six.moves.input(msg)
                config.ca_cert_path = ip
                print('')
                msg = 'Please Enter Path to the Edge Runtime ServerCertificate: '
                ip = six.moves.input(msg)
                config.edge_server_cert_path = ip
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                log.error(ex_val)
                print('Please try again.')

    @staticmethod
    def get_certificates_input(config):
        # Get certificate's data
        done = False
        while not done:
            try:
                print('')
                default_value = 'Yes'
                msg = 'Would you like to autogenerate the required' \
                      ' security certificates? Enter Yes/No ' \
                      '['+ default_value + ']: '
                ip = six.moves.input(msg)
                if ip is None or len(ip) == 0:
                    ip = default_value
                if ip == 'Yes':
                    config.security_option = \
                        EdgeHostConfig.security_option_self_signed
                    EdgeConfigInteractive.get_autogen_certificate_input(config)
                elif ip == 'No':
                    config.security_option = \
                        EdgeHostConfig.security_option_pre_installed
                    EdgeConfigInteractive.get_preinstalled_certificate_input(config)
                else:
                    raise ValueError('Invalid Response: ' + ip)
                done = True
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                log.error(ex_val)
                print('Please try again.')

    @staticmethod
    def get_deployment_input(config):
        deployments = EdgeDefault.get_supported_deployments()
        num_deployments = len(deployments)
        if num_deployments != 1:
            log.critical('Unexpected Deployment Configuration')
            sys.exit(1)
        config.deployment_type = deployments[0]
        if config.deployment_type == 'docker':
            config.deployment_config = EdgeDockerConfigInteractive.present()

    @staticmethod
    def get_log_level_input(config):
        done = False
        while not done:
            try:
                print('')
                default_value = 'info'
                msg = 'Setup Edge Runtime Diagnostic Log Level: ' \
                      '['+ default_value + ']: '
                ip = six.moves.input(msg)
                if ip is None or len(ip) == 0:
                    ip = default_value
                config.log_level = ip
                done = True
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                log.error(ex_val)
                print('Please try again.')

    @staticmethod
    def present():
        config = EdgeHostConfig()
        EdgeConfigInteractive.get_home_dir(config)
        EdgeConfigInteractive.get_connection_string(config)
        EdgeConfigInteractive.get_hostname(config)
        EdgeConfigInteractive.get_certificates_input(config)
        EdgeConfigInteractive.get_log_level_input(config)
        EdgeConfigInteractive.get_deployment_input(config)

        return config

class EdgeDockerConfigInteractive(object):
    @staticmethod
    def present():
        config = EdgeDeploymentConfigDocker()
        EdgeDockerConfigInteractive.get_uri(config)
        EdgeDockerConfigInteractive.get_edge_image(config)
        EdgeDockerConfigInteractive.add_repositories(config)

        return config

    @staticmethod
    def get_uri(config):
        done = False
        while not done:
            try:
                print('')
                default_value = EdgeDefault.get_platform_docker_uri()
                msg = 'Enter the Docker Daemon URI.\n' \
                      'Unless your Docker Daemon settings have changed,' \
                      'use this default setting. ' \
                      '['+ default_value + ']: '
                ip = six.moves.input(msg)
                if ip is None or len(ip) == 0:
                    ip = default_value
                config.uri = ip
                done = True
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                log.error(ex_val)
                print('Please try again.')

    @staticmethod
    def get_edge_image(config):
        done = False
        while not done:
            try:
                print('')
                data = EdgeDefault.get_default_user_input_config_json()
                default_value = data[EC.DEPLOYMENT_KEY]['docker']['edgeRuntimeImage']
                msg = 'Enter the Edge Runtime Image.' \
                      '['+ default_value + ']: '
                ip = six.moves.input(msg)
                if ip is None or len(ip) == 0:
                    ip = default_value
                config.edge_image = ip
                done = True
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                log.error(ex_val)
                print('Please try again.')

    @staticmethod
    def add_repository(config):
        done = False
        while not done:
            try:
                print('')
                msg = 'Enter Address: '
                ip = six.moves.input(msg)
                if ip is None or len(ip) == 0:
                    raise ValueError('Invalid Address: ' + ip)
                address = ip
                done = True
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                log.error(ex_val)
                print('Please try again.')

        done = False
        while not done:
            try:
                print('')
                msg = 'Enter Username: '
                ip = six.moves.input(msg)
                if ip is None or len(ip) == 0:
                    raise ValueError('Invalid Username: ' + ip)
                username = ip
                done = True
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                log.error(ex_val)
                print('Please try again.')

        done = False
        while not done:
            try:
                print('')
                msg = 'Enter Password: '
                ip = six.moves.input(msg)
                if ip is None or len(ip) == 0:
                    raise ValueError('Invalid Password: ' + ip)
                password = ip
                done = True
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                log.error(ex_val)
                print('Please try again.')

        try:
            config.add_registry(address, username, password)
        except ValueError as ex_val:
            log.error(ex_val)
            print('Please try again.')

    @staticmethod
    def add_repositories(config):
        done = False
        while not done:
            try:
                print('')
                default_value = 'Yes'
                msg = 'Would you add a docker repository? \n' \
                      'These need to be entered only if a username and' \
                      ' password are set. Enter Yes/No. ' \
                      '['+ default_value + ']: '
                ip = six.moves.input(msg)
                if ip is None or len(ip) == 0:
                    ip = default_value
                if ip == 'Yes':
                    EdgeDockerConfigInteractive.add_repository(config)
                elif ip == 'No':
                    done = True
                else:
                    raise ValueError('Invalid Response: ' + ip)
            except KeyboardInterrupt:
                print('\nExiting.\n')
                sys.exit(1)
            except ValueError as ex_val:
                log.error(ex_val)
                print('Please try again.')
