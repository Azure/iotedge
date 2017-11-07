import json
import logging as log
import edgectl.errors
from edgectl.edgeutils import EdgeUtils
from edgectl.edgeconfig import EdgeHostConfig
from edgectl.edgeconfig import EdgeDeploymentConfigDocker
from edgectl.edgeconfigparser import EdgeConfigParser
from edgectl.default import EdgeDefault
import edgectl.edgeconstants as EC


class EdgeConfigParserCLI(EdgeConfigParser):

    def __init__(self, args, deployment=None):
        if deployment is None:
            deployment = EC.DEPLOYMENT_DOCKER
        super(EdgeConfigParserCLI, self).__init__(args, deployment)

    def parse(self, ignored=None):
        args = self._input_args
        try:
            defaults_json = EdgeDefault.get_default_user_input_config_json()
            cs = args.connection_string
            if cs is None or len(cs) == 0:
                raise ValueError('Please specify the device connection string' \
                                 ' using the --connection-string option')

            config = EdgeHostConfig()
            config.schema_version = defaults_json[EC.SCHEMA_KEY]
            config.connection_string = cs

            home_dir = args.edge_home_dir
            if home_dir is None:
                home_dir = EdgeDefault.get_platform_home_dir()
            config.home_dir = home_dir

            hostname = args.edge_hostname
            if hostname is None:
                hostname = EdgeUtils.get_hostname()
            config.hostname = hostname

            log_level = args.runtime_log_level
            if log_level is None:
                log_level = EdgeDefault.edge_runtime_default_log_level()
            config.log_level = log_level

            # @todo get security options from user
            config.security_option = EC.SELFSIGNED_KEY
            config.self_signed_cert_option_force_no_passwords = \
                args.auto_cert_gen_force_no_passwords

            deploy_cfg = None
            if self._deployment_type == EC.DEPLOYMENT_DOCKER:
                deploy_cfg = EdgeDeploymentConfigDocker()
                docker_deploy_data = \
                    defaults_json[EC.DEPLOYMENT_KEY][EC.DEPLOYMENT_DOCKER_KEY]
                registries = args.docker_registries
                if registries is None:
                    registries = docker_deploy_data[EC.REGISTRIES_KEY]
                    for registry in registries:
                        deploy_cfg.add_registry(registry[EC.REGISTRY_ADDRESS_KEY],
                                                registry[EC.REGISTRY_USERNAME_KEY],
                                                registry[EC.REGISTRY_PASSWORD_KEY])
                else:
                    idx = 0
                    address = ''
                    username = ''
                    password = ''
                    for item in registries:
                        if idx == 0:
                            address = item
                        elif idx == 1:
                            username = item
                        else:
                            password = item
                            deploy_cfg.add_registry(address, username, password)
                        idx = (idx + 1) % 3

                image = args.image
                if image is None:
                    image = docker_deploy_data[EC.EDGE_RUNTIME_IMAGE_KEY]
                deploy_cfg.edge_image = image

                uri = args.docker_uri
                if uri is None:
                    uri = EdgeDefault.get_platform_docker_uri()
                deploy_cfg.uri = uri

                docker_log_cfg = docker_deploy_data[EC.DOCKER_LOGGING_OPTIONS_KEY]
                deploy_cfg.logging_driver = \
                    docker_log_cfg[EC.DOCKER_LOGGING_DRIVER_KEY]
                driver_log_opts = \
                    docker_log_cfg[EC.DOCKER_LOGGING_DRIVER_OPTIONS_KEY]
                for opt_key, opt_val in list(driver_log_opts.items()):
                    deploy_cfg.add_logging_option(opt_key, opt_val)

            if deploy_cfg is None:
                raise ValueError('Unsupported deployment type: %s', self._deployment_type)

            config.deployment_config = deploy_cfg

            return config
        except ValueError as ex_value:
            log.error('Error parsing user input data: %s.', str(ex_value))
            raise edgectl.errors.EdgeValueError('Error parsing user input data')
