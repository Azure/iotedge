import logging as log
from edgectl.config import EdgeConfigDirInputSource
from edgectl.config import EdgeConstants as EC
from edgectl.config import EdgeDeploymentConfigDocker
from edgectl.config import EdgeHostConfig
from edgectl.config import EdgeDefault
from edgectl.config import EdgeCertConfig
from edgectl.config.edgeconstants import EdgeUpstreamProtocol
import edgectl.errors
from edgectl.host import EdgeHostPlatform
from edgectl.utils import EdgeUtils
from edgectl.parser.edgeconfigparser import EdgeConfigParser


class EdgeConfigParserCLI(EdgeConfigParser):

    def __init__(self, args, deployment=None):
        if deployment is None:
            deployment = EC.DEPLOYMENT_DOCKER
        super(EdgeConfigParserCLI, self).__init__(args, deployment)

    def parse(self, ignored=None):
        args = self._input_args
        try:
            defaults_json = EdgeDefault.get_default_settings_json()
            cs = args.connection_string
            if cs is None or len(cs) == 0:
                raise ValueError('Please specify the device connection string' \
                                 ' using the --connection-string option')

            config = EdgeHostConfig()
            config.schema_version = defaults_json[EC.SCHEMA_KEY]
            config.connection_string = cs

            cfg_src = EdgeConfigDirInputSource.USER_PROVIDED
            cfg_dir_opt = EdgeHostPlatform.choose_platform_config_dir(args.edge_config_dir, cfg_src)

            config.config_dir = cfg_dir_opt[0]
            config.config_dir_source = cfg_dir_opt[1]

            home_dir = args.edge_home_dir
            if home_dir is None:
                home_dir = EdgeHostPlatform.get_home_dir()
            config.home_dir = home_dir

            hostname = args.edge_hostname
            if hostname is None:
                hostname = EdgeUtils.get_hostname()
            config.hostname = hostname

            log_level = args.runtime_log_level
            if log_level is None:
                log_level = EdgeDefault.get_default_runtime_log_level()
            config.log_level = log_level

            upstream_protocol = args.upstream_protocol
            if upstream_protocol is None:
                config.upstream_protocol = EdgeUpstreamProtocol.NONE
            else:
                config.upstream_protocol = upstream_protocol

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
                    uri = EdgeHostPlatform.get_docker_uri()
                deploy_cfg.uri = uri

                docker_log_cfg = docker_deploy_data[EC.DOCKER_LOGGING_OPTS_KEY]
                deploy_cfg.logging_driver = \
                    docker_log_cfg[EC.DOCKER_LOGGING_DRIVER_KEY]
                driver_log_opts = \
                    docker_log_cfg[EC.DOCKER_LOGGING_DRIVER_OPTS_KEY]
                for opt_key, opt_val in list(driver_log_opts.items()):
                    deploy_cfg.add_logging_option(opt_key, opt_val)

            if deploy_cfg is None:
                raise ValueError('Unsupported deployment type: %s', self._deployment_type)

            config.deployment_config = deploy_cfg

            subj_dict = {}
            if args.country:
                subj_dict[EC.SUBJECT_COUNTRY_KEY] = args.country
            if args.state:
                subj_dict[EC.SUBJECT_STATE_KEY] = args.state
            if args.locality:
                subj_dict[EC.SUBJECT_LOCALITY_KEY] = args.locality
            if args.organization:
                subj_dict[EC.SUBJECT_ORGANIZATION_KEY] = args.organization
            if args.organization_unit:
                subj_dict[EC.SUBJECT_ORGANIZATION_UNIT_KEY] = args.organization_unit
            if args.common_name:
                subj_dict[EC.SUBJECT_COMMON_NAME_KEY] = args.common_name

            cert_config = EdgeCertConfig()
            cert_config.set_options(args.auto_cert_gen_force_no_passwords,
                                    subj_dict,
                                    owner_ca_cert_file=args.owner_ca_cert_file,
                                    device_ca_cert_file=args.device_ca_cert_file,
                                    device_ca_chain_cert_file=args.device_ca_chain_cert_file,
                                    device_ca_private_key_file=args.device_ca_private_key_file,
                                    device_ca_passphrase=args.device_ca_passphrase,
                                    device_ca_passphrase_file=args.device_ca_passphrase_file,
                                    agent_ca_passphrase=args.agent_ca_passphrase,
                                    agent_ca_passphrase_file=args.agent_ca_passphrase_file)
            config.certificate_config = cert_config
            return config
        except ValueError as ex_value:
            log.error('Error parsing user input data: %s.', str(ex_value))
            raise edgectl.errors.EdgeValueError('Error parsing user input data')
