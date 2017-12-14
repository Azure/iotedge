import json
import logging as log
import edgectl.errors
import edgectl.edgeconstants as EC
from edgectl.default import EdgeDefault
from edgectl.edgeconfig import EdgeHostConfig
from edgectl.edgeconfig import EdgeDeploymentConfigDocker
from edgectl.edgeconfigparser import EdgeConfigParser


class EdgeConfigParserFile(EdgeConfigParser):

    def __init__(self, args):
        super(EdgeConfigParserFile, self).__init__(args, None)

    def parse(self, config_file=None):
        try:
            if config_file is None:
                config_file = self._input_args.config_file
            with open(config_file, 'r') as edge_config:
                data = json.load(edge_config)
        except ValueError as ex_value:
            log.error('JSON Parse Error: %s', str(ex_value))
            log.error('Bad format for Edge config file: %s.', config_file)
            raise edgectl.errors.EdgeFileParseError('Error parsing file', config_file)
        except IOError as ex_os:
            log.error('Error reading config file: %s. Errno: %s, Error: %s',
                      config_file, str(ex_os.errno), ex_os.strerror)
            raise edgectl.errors.EdgeFileAccessError('Cannot read file', config_file)
        return self._parse_data(data, config_file)

    def _parse_data(self, data, config_file):
        try:
            config = EdgeHostConfig()
            config.schema_version = data[EC.SCHEMA_KEY]
            config.connection_string = data[EC.DEVICE_CONNECTION_STRING_KEY]

            edge_config_dir = None
            cfg_src = EC.EdgeConfigDirInputSource.NONE
            if EC.CONFIG_DIR_KEY in list(data.keys()):
                edge_config_dir = data[EC.CONFIG_DIR_KEY]
                cfg_src = EC.EdgeConfigDirInputSource.USER_PROVIDED

            cfg_dir_opt = EdgeDefault.choose_platform_config_dir(edge_config_dir, cfg_src)
            config.config_dir = cfg_dir_opt[0]
            config.config_dir_source = cfg_dir_opt[1]

            config.home_dir = data[EC.HOMEDIR_KEY]
            config.hostname = data[EC.HOSTNAME_KEY]
            config.log_level = data[EC.EDGE_RUNTIME_LOG_LEVEL_KEY]
            # parse certificate settings
            certs_cfg_data = data[EC.SECURITY_KEY][EC.CERTS_KEY]
            security_option = certs_cfg_data[EC.CERTS_OPTION_KEY]
            subject_dict = {}
            if EC.CERTS_SUBJECT_KEY in list(certs_cfg_data.keys()):
                subject_dict = certs_cfg_data[EC.CERTS_SUBJECT_KEY]
            if security_option == EC.SELFSIGNED_KEY:
                ss_cert_data = certs_cfg_data[EC.SELFSIGNED_KEY]
                dev_pass_file = None
                agt_pass_file = None
                if EC.DEVICE_CA_PASSPHRASE_FILE_KEY in list(ss_cert_data.keys()):
                    dev_pass_file = ss_cert_data[EC.DEVICE_CA_PASSPHRASE_FILE_KEY]
                if EC.AGENT_CA_PASSPHRASE_FILE_KEY in list(ss_cert_data.keys()):
                    agt_pass_file = ss_cert_data[EC.AGENT_CA_PASSPHRASE_FILE_KEY]
                config.set_security_options(ss_cert_data[EC.FORCENOPASSWD_KEY],
                                            subject_dict,
                                            device_ca_passphrase_file=dev_pass_file,
                                            agent_ca_passphrase_file=agt_pass_file)
            else:
                pre_install = certs_cfg_data[EC.PREINSTALLED_KEY]
                dev_pass_file = pre_install[EC.DEVICE_CA_PASSPHRASE_FILE_KEY]
                agt_pass_file = pre_install[EC.AGENT_CA_PASSPHRASE_FILE_KEY]
                owner_ca_cert_file = pre_install[EC.PREINSTALLED_OWNER_CA_CERT_FILE_KEY]
                dev_ca_cert_file = pre_install[EC.PREINSTALLED_DEVICE_CA_CERT_FILE_KEY]
                dev_ca_chain_cert_file = pre_install[EC.PREINSTALLED_DEVICE_CA_CHAIN_CERT_FILE_KEY]
                dev_ca_pk_file = pre_install[EC.PREINSTALLED_DEVICE_CA_PRIVATE_KEY_FILE_KEY]
                config.set_security_options(pre_install[EC.FORCENOPASSWD_KEY],
                                            subject_dict,
                                            device_ca_passphrase_file=dev_pass_file,
                                            agent_ca_passphrase_file=agt_pass_file,
                                            owner_ca_cert_file=owner_ca_cert_file,
                                            device_ca_cert_file=dev_ca_cert_file,
                                            device_ca_chain_cert_file=dev_ca_chain_cert_file,
                                            device_ca_private_key_file=dev_ca_pk_file)

            docker_cfg = None
            deployment_type = data[EC.DEPLOYMENT_KEY][EC.DEPLOYMENT_TYPE_KEY]
            if deployment_type == EC.DEPLOYMENT_DOCKER_KEY:
                docker_cfg = data[EC.DEPLOYMENT_KEY][EC.DEPLOYMENT_DOCKER_KEY]
                deploy_cfg = EdgeDeploymentConfigDocker()
                deploy_cfg.uri = docker_cfg[EC.DOCKER_URI_KEY]
                deploy_cfg.edge_image = docker_cfg[EC.EDGE_RUNTIME_IMAGE_KEY]
                for reg in docker_cfg[EC.REGISTRIES_KEY]:
                    deploy_cfg.add_registry(reg[EC.REGISTRY_ADDRESS_KEY],
                                            reg[EC.REGISTRY_USERNAME_KEY],
                                            reg[EC.REGISTRY_PASSWORD_KEY])
                docker_log_cfg = docker_cfg[EC.DOCKER_LOGGING_OPTIONS_KEY]
                deploy_cfg.logging_driver = \
                    docker_log_cfg[EC.DOCKER_LOGGING_DRIVER_KEY]
                log_opts = docker_log_cfg[EC.DOCKER_LOGGING_DRIVER_OPTIONS_KEY]
                for opt_key, opt_val in list(log_opts.items()):
                    deploy_cfg.add_logging_option(opt_key, opt_val)

            if docker_cfg is None:
                raise ValueError('Unsupported deployment type: %s' % (deployment_type))
            config.deployment_config = deploy_cfg
            self._deployment_type = deployment_type
            result = config
        except ValueError as ex_value:
            log.error('Error when parsing config data from file: %s. %s.',
                      config_file, str(ex_value))
            raise edgectl.errors.EdgeValueError('Error when parsing config file: %s', config_file)

        return result
