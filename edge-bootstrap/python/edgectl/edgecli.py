from __future__ import print_function
import argparse
import logging as log
import sys
import platform
import edgectl.errors
from edgectl.edgehostplatform import EdgeHostPlatform
from edgectl.edgeconfiginteractive import EdgeConfigInteractive
from edgectl.edgeconfigparserfactory import EdgeConfigParserFactory
from edgectl.commandfactory import EdgeCommandFactory
from edgectl.default import EdgeDefault
from edgectl.edgeutils import EdgeUtils
import edgectl.edgeconstants as EC


class EdgeCLI(object):
    """This class implements the CLI to control the Azure Edge Runtime Control.
    """
    _PROGRAM_NAME = ''
    _SUPPORTED_LOG_LEVELS = ['DEBUG', 'INFO', 'WARNING', 'ERROR']
    _LOG_LEVEL_CHOICES = ['DEBUG', 'INFO', 'WARNING', 'ERROR', \
                           'debug', 'info', 'warning', 'error']
    _DEFAULT_LOG_VERBOSE_LEVEL = 'INFO'
    _LOGGING_INITIALIZED = False
    _DEFAULT_REGISTRY_ADDRESS = 'https://index.docker.io/v1/'

    def __init__(self, program_name, version):
        EdgeCLI._PROGRAM_NAME = program_name
        self._version_str = version
        self._user_command = None
        self._user_edge_config = None
        self._verbose_level_str = EdgeCLI._default_log_level()
        self._initialized_log = False
        self._deployment_type = EC.DEPLOYMENT_DOCKER

    def execute_user_command(self):
        """This is the main function that implements the CLI.

        Overall flow:
        1. Read, validate and process the input arguments.
        2. Update configuration files on the host based on user input.
        3. Construct the deployment specific command object to execute the
           the user input command.
        4. Log any status, progress and errors along the way to stdout
        5. Return to caller with an error code.
            0 -- Success
            Non Zero -- Error
        """
        error_code = 1
        if EdgeDefault.is_platform_supported() is False:
            log.error('Exiting. Return Code: %s', str(error_code))
        else:
            try:
                (is_valid, execute_deployment_cmd) = self._process_cli_args()
                if is_valid and execute_deployment_cmd:
                    self._execute_command()
                    error_code = 0
            except edgectl.errors.EdgeError as ex:
                log.error('Errors were observed. Return Code: %s', str(error_code))
        return error_code

    def _execute_command(self):
        log.debug('Executing command \'%s\'', self._command)
        edge_cmd = EdgeCommandFactory.create_command(self._command,
                                                     self.edge_config)
        edge_cmd.execute()
        return

    @staticmethod
    def _prog():
        return EdgeCLI._PROGRAM_NAME

    @staticmethod
    def _supported_log_levels():
        return EdgeCLI._SUPPORTED_LOG_LEVELS

    @staticmethod
    def _log_level_choices():
        return EdgeCLI._LOG_LEVEL_CHOICES

    @staticmethod
    def _default_log_level():
        return EdgeCLI._DEFAULT_LOG_VERBOSE_LEVEL

    @staticmethod
    def _setup_logging(level):
        if EdgeCLI._LOGGING_INITIALIZED is False:
            EdgeCLI._LOGGING_INITIALIZED = True
            format = '%(levelname)s: %(message)s'
            log.basicConfig(format=format,
                            level=getattr(log, level))
            """
            Workaround to avoid red error text for INFO, DEBUG, and WARNING messages in remote PowerShell
            """
            if platform.system().lower() == 'windows':
                class ExclusiveMaxFilter(log.Filter):
                    def __init__(self, level):
                        self._level = level
                    def filter(self, rec):
                        return rec.levelno < self._level

                log.getLogger().handlers = []

                outHandler = log.StreamHandler(sys.stdout)
                outHandler.addFilter(ExclusiveMaxFilter(log.ERROR))
                outHandler.setFormatter(log.Formatter(format))
                log.getLogger().addHandler(outHandler)

                errHandler = log.StreamHandler(sys.stderr)
                errHandler.setLevel(log.ERROR)
                errHandler.setFormatter(log.Formatter(format))
                log.getLogger().addHandler(errHandler)

    @property
    def _verbose_level(self):
        return self._verbose_level_str

    @_verbose_level.setter
    def _verbose_level(self, value):
        self._verbose_level_str = value
        EdgeCLI._setup_logging(value)

    @property
    def edge_config(self):
        return self._user_edge_config

    @edge_config.setter
    def edge_config(self, value):
        self._user_edge_config = value

    @property
    def _command(self):
        return self._user_command

    @_command.setter
    def _command(self, value):
        self._user_command = value

    @property
    def _deployment(self):
        return self._deployment_type

    def _version(self):
        return self._version_str

    def _process_cli_args(self):
        parser = argparse.ArgumentParser(prog=EdgeCLI._prog(),
                                         formatter_class=argparse.RawTextHelpFormatter,
                                         description='Azure IoT Edge Runtime Control Interface',
                                         epilog='''''')
        parser.add_argument('--version',
                            action='version',
                            version='{0} {1}'.format(EdgeCLI._prog(), self._version()))

        verbose_help_str = 'Set verbosity. Levels: ' \
                           + ', '.join(EdgeCLI._supported_log_levels()) \
                           + '. Default: ' + EdgeCLI._default_log_level()

        parser.add_argument('--verbose', dest='verbose_level',
                            choices=EdgeCLI._log_level_choices(),
                            default=EdgeCLI._default_log_level(),
                            help=verbose_help_str, metavar='')

        subparsers = parser.add_subparsers(title='commands',
                                           description='Azure IoT Edge Commands',
                                           help='sub-command help',
                                           dest='subparser_name')

        cmd_setup = subparsers.add_parser('setup',
                                          description='Setup the runtime. This must be run before starting.',
                                          help='Setup the runtime. This must be run before starting.',
                                          formatter_class=argparse.RawTextHelpFormatter)

        cmd_setup.add_argument('--config-file',
                               help='Setup the runtime using the specified configuration file. Optional.\n'
                               + 'If specified, all other command line inputs will be ignored.',
                               metavar='')

        cmd_setup.add_argument('--connection-string',
                               help='Set the Azure IoT Hub device connection string. Required.\n'
                               + 'Note: Use double quotes when supplying this input.',
                               metavar='')

        cmd_setup.add_argument('--edge-home-dir',
                               help='Set runtime home directory. Optional.\n'
                               + 'Default:\n'
                               + '   Linux Hosts - ' + EdgeDefault.get_home_dir(EC.DOCKER_HOST_LINUX) + '\n'
                               + '   Windows Hosts - ' + EdgeDefault.get_home_dir(EC.DOCKER_HOST_WINDOWS),
                               metavar='')

        cmd_setup.add_argument('--edge-hostname',
                               help='Set the runtime hostname (FQDN). Optional.\n'
                               + 'Used when operating the runtime as a \'Gateway\' for leaf devices.',
                               metavar='')

        log_levels = EdgeDefault.edge_runtime_log_levels()
        log_levels = ", ".join(log_levels)
        cmd_setup.add_argument('--runtime-log-level',
                               help='Set runtime log level. Optional.\n'
                               + 'Levels:  ' + log_levels + '\n'
                               + 'Default: ' + EdgeDefault.edge_runtime_default_log_level(),
                               metavar='')

        cmd_setup.add_argument('--image',
                               help='Set the Edge Agent image. Optional.\n',
                               metavar='')

        cmd_setup.add_argument('--docker-registries',
                               help='Set a list of registries and their credentials. Optional.\n'
                               + 'Specified as triples of registry address, username, password.\n'
                               + 'Example: --docker-registries reg1 user1 pass1'
                               , nargs='+', metavar='')

        cmd_setup.add_argument('--docker-uri',
                               help='Set docker endpoint uri. Optional.\n'
                               + 'Default:\n'
                               + '   Linux Hosts - ' + EdgeDefault.docker_uri(EC.DOCKER_HOST_LINUX, EC.DOCKER_ENGINE_LINUX) + '\n'
                               + '   Windows Hosts (Linux VM) - ' + EdgeDefault.docker_uri(EC.DOCKER_HOST_WINDOWS, EC.DOCKER_ENGINE_LINUX) + '\n'
                               + '   Windows Hosts (Native) - ' + EdgeDefault.docker_uri(EC.DOCKER_HOST_WINDOWS, EC.DOCKER_ENGINE_WINDOWS),
                               metavar='')

        cmd_setup.add_argument('--auto-cert-gen-force-no-passwords',
                               help='Do not prompt for passwords when generating private keys. Optional.',
                               action='store_true')

        cmd_setup.add_argument('--owner-ca-cert-file',
                               help='Owner CA certificate in X.509 PEM format.\n' \
                               'Used when operating the runtime as a \'Gateway\' for leaf devices. Optional.',
                               metavar='')

        cmd_setup.add_argument('--device-ca-cert-file',
                               help='Device CA certificate in X.509 PEM format.\n' \
                               'Used when operating the runtime as a \'Gateway\' for leaf devices. Optional.',
                               metavar='')

        cmd_setup.add_argument('--device-ca-chain-cert-file',
                               help='Device CA chain certificate in X.509 PEM format.\n' \
                               'Used when operating the runtime as a \'Gateway\' for leaf devices. Optional.',
                               metavar='')

        cmd_setup.add_argument('--device-ca-private-key-file',
                               help='Device CA certificate private key file in PEM format.\n' \
                               'Used when operating the runtime as a \'Gateway\' for leaf devices. Optional.',
                               metavar='')

        cmd_setup.add_argument('--device-ca-passphrase-file',
                               help='Device CA certificate private key passphrase file in ascii text.\n' \
                               'Users can provide this file or use the --device-ca-passphrase option but not both.',
                               metavar='')

        cmd_setup.add_argument('--device-ca-passphrase',
                               help='Device CA certificate private key passphrase in ascii text.\n' \
                               'Users can use this option to provide the passphrase or use the --device-ca-passphrase-file option but not both.',
                               metavar='')

        cmd_setup.add_argument('--agent-ca-passphrase-file',
                               help='Agent CA certificate private key passphrase file in ascii text.\n' \
                               'Users can provide this file or use the --agent-ca-passphrase option but not both.',
                               metavar='')

        cmd_setup.add_argument('--agent-ca-passphrase',
                               help='Agent CA certificate private key passphrase in ascii text.\n' \
                               'Users can use this option to provide the passphrase or use the --agent-ca-passphrase-file option but not both.',
                               metavar='')

        subj = EdgeDefault.certificate_subject_dict()
        cmd_setup.add_argument('-C', '--country',
                               help='Two letter country code. This parameter is used when autogenerating certificates. Optional.\n'
                               'Default: \'{0}\''.format(subj[EC.SUBJECT_COUNTRY_KEY]), metavar='')

        cmd_setup.add_argument('-ST', '--state',
                               help='State. This parameter is used when autogenerating certificates. Optional.\n'
                               'Default: \'{0}\''.format(subj[EC.SUBJECT_STATE_KEY]), metavar='')

        cmd_setup.add_argument('-L', '--locality',
                               help='Locality or city. This parameter is used when autogenerating certificates. Optional.\n'
                               'Default: \'{0}\''.format(subj[EC.SUBJECT_LOCALITY_KEY]), metavar='')

        cmd_setup.add_argument('-OR', '--organization',
                               help='Organization name. This parameter is used when autogenerating certificates. Optional.\n'
                               'Default: \'{0}\''.format(subj[EC.SUBJECT_ORGANIZATION_KEY]), metavar='')

        cmd_setup.add_argument('-OU', '--organization-unit',
                               help='Organization unit name. This parameter is used when autogenerating certificates. Optional.\n'
                               'Default: \'{0}\''.format(subj[EC.SUBJECT_ORGANIZATION_UNIT_KEY]), metavar='')

        cmd_setup.add_argument('-CN', '--common-name',
                               help='Common name used for the Device CA certificate.' \
                               'This parameter is used when autogenerating certificates. Optional.\n'
                               'Default: \'{0}\'. ' \
                               .format(subj[EC.SUBJECT_COMMON_NAME_KEY]),
                               metavar='')

        cmd_setup.set_defaults(func=self._parse_edge_command)

        cmd_start = subparsers.add_parser('start', description="Start the runtime.", help='Start the runtime.')
        cmd_start.set_defaults(func=self._parse_edge_command)

        cmd_restart = subparsers.add_parser('restart', description='Restart the runtime.', help='Restart the runtime.')
        cmd_restart.set_defaults(func=self._parse_edge_command)

        cmd_stop = subparsers.add_parser('stop', description='Stop the runtime.', help='Stop the runtime.')
        cmd_stop.set_defaults(func=self._parse_edge_command)

        cmd_status = subparsers.add_parser('status', description='Report the status of the runtime.', help='Report the status of the runtime.')
        cmd_status.set_defaults(func=self._parse_edge_command)

        cmd_update = subparsers.add_parser('update', description='Update the Edge Agent image.', help='Update the Edge Agent image.')
        cmd_update.add_argument('--image', help='Specify the Edge Agent image', required = True, metavar='')
        cmd_update.set_defaults(func=self._parse_edge_command)

        cmd_uninstall = subparsers.add_parser('uninstall', description='Remove all modules and generated files.',
                                              help='Remove all modules and generated files.')
        cmd_uninstall.set_defaults(func=self._parse_edge_command)

        cmd_login = subparsers.add_parser('login', description="Log in to a container registry.",
                                          help='Log in to a container registry.',
                                          formatter_class=argparse.RawTextHelpFormatter)
        cmd_login.add_argument('--address', help='Specify the container registry. (e.g. example.azurecr.io)\nDefault: Docker Hub',
                               required=False, default=EdgeCLI._DEFAULT_REGISTRY_ADDRESS)
        cmd_login.add_argument('--username', help='Specify the username of the container registry', required=True)
        cmd_login.add_argument('--password', help='Specify the password of the container registry', required=True)
        cmd_login.set_defaults(func=self._parse_edge_command)

        args = parser.parse_args()
        if 'func' in vars(args):
            return args.func(args)
        else:
            parser.print_usage()
            return (False, False)

    def _parse_edge_command(self, args):
        args.verbose_level = args.verbose_level.upper()
        self._verbose_level = args.verbose_level
        commands = {
            'setup' : self._parse_setup_options,
            'start' : self._parse_command_options_common,
            'restart' : self._parse_command_options_common,
            'stop' : self._parse_command_options_common,
            'uninstall' : self._parse_uninstall_options,
            'status' : self._parse_command_options_common,
            'update' : self._parse_update_options,
            'login' : self._parse_login_options
        }
        self._command = args.subparser_name
        is_valid = False
        execute_deployment_cmd = False
        if EdgeDefault.is_deployment_supported(self._deployment):
            (is_valid, execute_deployment_cmd) = commands[args.subparser_name](args)
        else:
            log.critical('IoT Edge dependency not available: %s', self._deployment)
        return (is_valid, execute_deployment_cmd)

    def _parse_command_options_common(self, args):
        cmd = args.subparser_name
        log.debug('Command: %s', cmd)
        try:
            is_valid = self._parse_installed_config_file_options(args)
            execute_deployment_cmd = False
            if is_valid:
                execute_deployment_cmd = True
        except edgectl.errors.EdgeFileAccessError as ex_access:
            err_msg = 'Error observed when executing command: {0}'.format(cmd)
            raise edgectl.errors.EdgeError(err_msg, ex_access)
        except edgectl.errors.EdgeValueError as ex_value:
            log.error('To fix this error, please re-run \'%s setup\'.', EdgeCLI._prog())
            raise edgectl.errors.EdgeError('Incorrect configuration data', ex_value)
        except edgectl.errors.EdgeFileParseError as ex_parse:
            log.error('To fix this error, please re-run \'%s setup\'.', EdgeCLI._prog())
            raise edgectl.errors.EdgeError('Error when parsing configuration data', ex_parse)

        return (is_valid, execute_deployment_cmd)

    def _parse_uninstall_options(self, args):
        (is_valid, execute_deployment_cmd) = self._parse_command_options_common(args)
        if is_valid:
            EdgeHostPlatform.uninstall_edge(self.edge_config.home_dir)
        return (is_valid, execute_deployment_cmd)

    def _parse_installed_config_file_options(self, args):
        result = False
        ins_cfg_file_path = EdgeHostPlatform.get_host_config_file_path()
        if ins_cfg_file_path is None:
            log.error('Runtime has not been configured on this device.\nPlease run \'%s setup\' first.', EdgeCLI._prog())
        else:
            log.debug('Found config File: %s', ins_cfg_file_path)
            ip_type = EC.EdgeConfigInputSources.FILE
            parser = EdgeConfigParserFactory.create_parser(ip_type, args)
            self.edge_config = parser.parse(ins_cfg_file_path)
            result = True
        return result

    def _parse_and_validate_user_input_config_file(self, args):
        ip_type = EC.EdgeConfigInputSources.FILE
        parser = EdgeConfigParserFactory.create_parser(ip_type, args)
        self.edge_config = parser.parse()
        EdgeHostPlatform.install_edge_by_config_file(self.edge_config,
                                                     args.config_file)
        return True

    def _parse_and_validate_user_input(self, args):
        ip_type = EC.EdgeConfigInputSources.CLI
        parser = EdgeConfigParserFactory.create_parser(ip_type, args)
        self.edge_config = parser.parse()
        EdgeHostPlatform.install_edge_by_json_data(self.edge_config, True)
        return True

    def _parse_update_options(self, args):
        (is_valid, execute_deployment_cmd) = self._parse_command_options_common(args)
        if is_valid:
            if self.edge_config.deployment_config.edge_image == args.image:
                log.info('New Edge Agent image matches existing. Skipping update.')
                execute_deployment_cmd = False
            else:
                try:
                    self.edge_config.deployment_config.edge_image = args.image
                except ValueError as ex:
                    log.error('%s', str(ex))
                    log.error('Error setting --image data: %s.', args.image)
                    raise edgectl.errors.EdgeError('Error setting Edge Agent image', ex)
                EdgeHostPlatform.install_edge_by_json_data(self.edge_config, False)
                config_file = EdgeHostPlatform.get_host_config_file_path()
                log.info('The runtime configuration file %s was updated with' \
                         ' the new image: %s', config_file, args.image)

        return (is_valid, execute_deployment_cmd)

    def _parse_login_options(self, args):
        (is_valid, execute_deployment_cmd) = self._parse_command_options_common(args)
        if is_valid:
            try:
                self.edge_config.deployment_config.add_registry(args.address,
                                                                args.username,
                                                                args.password)
            except ValueError as ex:
                log.error('%s', str(ex))
                msg = EdgeUtils.sanitize_registry_data(args.address,
                                                       args.username,
                                                       args.password)
                log.error('Error setting login data: [%s].', msg)
                raise edgectl.errors.EdgeError('Error setting login data', ex)

            EdgeHostPlatform.install_edge_by_json_data(self.edge_config, False)
            config_file = EdgeHostPlatform.get_host_config_file_path()
            log.info('The runtime configuration file %s was updated with' \
                     ' the credentials for registry: %s', config_file, args.address)

        return (is_valid, execute_deployment_cmd)

    def _parse_setup_options(self, args):
        cmd = args.subparser_name
        log.debug('Command: ' + cmd)

        if args.config_file is not None:
            # we are using options specified in the config file
            try:
                is_valid = self._parse_and_validate_user_input_config_file(args)
            except edgectl.errors.EdgeValueError as ex_value:
                log.error('Please check the configuration values in the config file' \
                          ' and re-run \'%s setup\'', EdgeCLI._prog())
                raise edgectl.errors.EdgeError('Error when parsing configuration data', ex_value)
            except edgectl.errors.EdgeFileParseError as ex_parse:
                log.error('Please check the configuration in config file: %s' \
                          ' and re-run \'%s setup\'', ex_parse.file_name, EdgeCLI._prog())
                raise edgectl.errors.EdgeError('Error when parsing configuration data', ex_parse)
        else:
            # we are using cli options, validate all the supplied args
            try:
                is_valid = self._parse_and_validate_user_input(args)
            except edgectl.errors.EdgeValueError as ex_value:
                log.error('Please fix any input values and re-run \'%s setup\'', EdgeCLI._prog())
                raise edgectl.errors.EdgeError('Incorrect input options', ex_value)
            except edgectl.errors.EdgeFileParseError as ex_parse:
                log.critical('Please restore the config file or reinstall the %s utility.', EdgeCLI._prog())
                raise edgectl.errors.EdgeError('Error when parsing configuration data', ex_parse)
            except edgectl.errors.EdgeFileAccessError as ex_access:
                if (ex_access.file_name == EdgeDefault.default_user_input_config_abs_file_path()):
                    log.critical('Please restore the config file or reinstall the %s utility.', EdgeCLI._prog())
                raise edgectl.errors.EdgeError('Filesystem access errors', ex_access)
        if is_valid:
            config_file = EdgeHostPlatform.get_host_config_file_path()
            log.info('The runtime configuration file %s was updated with' \
                     ' the ''setup'' options.', config_file)
        return (is_valid, is_valid)
