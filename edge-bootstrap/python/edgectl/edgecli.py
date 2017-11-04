import argparse
import edgectl
import logging as log
import pkg_resources
import sys
from edgectl.edgehostplatform import EdgeHostPlatform
from edgectl.edgeconfiginteractive import EdgeConfigInteractive
from edgectl.edgeconfigparserfactory import EdgeConfigParserFactory
from edgectl.commandfactory import EdgeCommandFactory
from edgectl.default import EdgeDefault
import edgectl.edgeconstants as EC


class EdgeCLI(object):
    _prog = ''
    _supported_log_levels = ['DEBUG', 'INFO', 'WARNING', 'ERROR']
    _choices_log_levels = ['DEBUG', 'INFO', 'WARNING', 'ERROR', \
                           'debug', 'info', 'warning', 'error']
    _default_log_verbose_level = 'INFO'
    _initialized_logging = False

    def __init__(self, program_name):
        EdgeCLI._prog = program_name
        self._command = None
        self._user_edge_config = None
        self._verbose_level = EdgeCLI.default_log_level()
        self._initialized_log = False

    @staticmethod
    def prog():
        return EdgeCLI._prog

    @staticmethod
    def supported_log_levels():
        return EdgeCLI._supported_log_levels

    @staticmethod
    def log_level_choices():
        return EdgeCLI._choices_log_levels

    @staticmethod
    def default_log_level():
        return EdgeCLI._default_log_verbose_level

    @staticmethod
    def setup_logging(level):
        if EdgeCLI._initialized_logging is False:
            EdgeCLI._initialized_logging = True
            log.basicConfig(format='%(levelname)s: %(message)s',
                            level=getattr(log, level))

    @staticmethod
    def exit(code):
        sys.exit(code)

    @property
    def verbose_level(self):
        return self._verbose_level

    @verbose_level.setter
    def verbose_level(self, value):
        self._verbose_level = value
        EdgeCLI.setup_logging(value)

    @property
    def edge_config(self):
        return self._user_edge_config

    @edge_config.setter
    def edge_config(self, value):
        self._user_edge_config = value

    @property
    def command(self):
        return self._command

    @command.setter
    def command(self, value):
        self._command = value

    def version(self):
        return pkg_resources.require(edgectl.package_name)[0].version

    def process_cli_args(self):
        parser = argparse.ArgumentParser(prog=EdgeCLI.prog(),
                                         usage=EdgeCLI.prog() + ' [command] [options]',
                                         formatter_class=argparse.RawTextHelpFormatter,
                                         description='Azure IoT Edge Runtime Control Interface',
                                         epilog='''''')
        parser.add_argument('--version',
                            action='version',
                            version='%s %s' % (EdgeCLI.prog(), self.version(),))

        verbose_help_str = 'Set verbosity. Levels: ' \
                           + ', '.join(EdgeCLI.supported_log_levels()) \
                           + '. Default: ' + EdgeCLI.default_log_level()

        parser.add_argument('--verbose', dest='verbose_level',
                            choices=EdgeCLI.log_level_choices(),
                            default=EdgeCLI.default_log_level(),
                            help=verbose_help_str, metavar='')

        subparsers = parser.add_subparsers(title='commands',
                                           description='Azure IoT Edge Commands',
                                           help='sub-command help',
                                           dest='subparser_name')

        cmd_setup = subparsers.add_parser('setup', description='Setup the runtime. This must be run before starting.'
                                          , help='Setup the runtime. This must be run before starting.',
                                          formatter_class=argparse.RawTextHelpFormatter)

        cmd_setup.add_argument('--config-file',
                               help='Setup the runtime using the specified configuration file. Optional.'
                               , metavar='')

        cmd_setup.add_argument('--interactive',
                               help='Setup the runtime interactively. Optional.'
                               , action='store_true')

        cmd_setup.add_argument('--connection-string',
                               help='Set the Azure IoT Hub device connection string. Required.\n'
                               + 'Note: Use double quotes when supplying this input.'
                               , metavar='')

        cmd_setup.add_argument('--edge-home-dir',
                               help='Set runtime home directory. Optional.\n'
                               + 'Default:\n'
                               + '   Linux Hosts - ' + EdgeDefault.get_home_dir(EC.DOCKER_HOST_LINUX) + '\n'
                               + '   Windows Hosts - ' + EdgeDefault.get_home_dir(EC.DOCKER_HOST_WINDOWS)
                               , metavar='')

        cmd_setup.add_argument('--edge-hostname',
                               help='Set the runtime hostname (FQDN). Optional.\n'
                               + 'Used when operating the runtime as a \'Gateway\' for leaf devices.'
                               , metavar='')

        log_levels = EdgeDefault.edge_runtime_log_levels()
        log_levels = ", ".join(log_levels)
        cmd_setup.add_argument('--runtime-log-level',
                               help='Set runtime log level. Optional.\n'
                               + 'Levels:  ' + log_levels + '\n'
                               + 'Default: ' + EdgeDefault.edge_runtime_default_log_level()
                               , metavar='')

        cmd_setup.add_argument('--image',
                               help='Set the Edge Agent image. Optional.\n'
                               , metavar='')

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
                               + '   Windows Hosts (Native) - ' + EdgeDefault.docker_uri(EC.DOCKER_HOST_WINDOWS, EC.DOCKER_ENGINE_WINDOWS)
                               , metavar='')

        cmd_setup.add_argument('--auto-cert-gen-force-no-passwords',
                               help='Do not use passwords for private keys for a no prompt experience. Optional.'
                               , action='store_true')

        cmd_setup.set_defaults(func=self.parse_edge_command)

        cmd_start = subparsers.add_parser('start', description="Start the runtime.", help='Start the runtime.')
        cmd_start.set_defaults(func=self.parse_edge_command)

        cmd_restart = subparsers.add_parser('restart', description='Restart the runtime.', help='Restart the runtime.')
        cmd_restart.set_defaults(func=self.parse_edge_command)

        cmd_stop = subparsers.add_parser('stop', description='Stop the runtime.', help='Stop the runtime.')
        cmd_stop.set_defaults(func=self.parse_edge_command)

        cmd_status = subparsers.add_parser('status', description='Report the status of the runtime.', help='Report the status of the runtime.')
        cmd_status.set_defaults(func=self.parse_edge_command)

        cmd_update = subparsers.add_parser('update', description='Update the Edge Agent image.', help='Update the Edge Agent image.')
        cmd_update.add_argument('--image', help='Specify the Edge Agent image', metavar='')

        cmd_update.set_defaults(func=self.parse_edge_command)

        cmd_uninstall = subparsers.add_parser('uninstall', description='Remove all modules and generated files.'
                                              , help='Remove all modules and generated files.')
        cmd_uninstall.set_defaults(func=self.parse_edge_command)

        cmd_login = subparsers.add_parser('login', description="Log in to a container registry."
                                          , help='Log in to a container registry.')
        cmd_login.add_argument('--username',
                               help='Specify the username of the container registry',
                               metavar='')
        cmd_login.add_argument('--password',
                               help='Specify the password of the container registry',
                               metavar='')
        cmd_login.add_argument('--address',
                               help='Specify the address of the container registry. (e.g example.azurecr.io)',
                               metavar='')

        cmd_login.set_defaults(func=self.parse_edge_command)

        args = parser.parse_args()

        result = False
        try:
            args.func(args)
            result = True
        except ValueError:
            raise

        return result

    def parse_edge_command(self, args):
        args.verbose_level = args.verbose_level.upper()
        self.verbose_level = args.verbose_level
        commands = {
            'setup' : self.parse_setup_options,
            'start' : self.parse_command_options_common,
            'restart' : self.parse_command_options_common,
            'stop' : self.parse_command_options_common,
            'uninstall' : self.parse_uninstall_options,
            'status' : self.parse_command_options_common,
            'update' : self.parse_update_options,
            'login' : self.parse_login_options
        }
        self.command = args.subparser_name
        commands[args.subparser_name](args)
        return

    def parse_command_options_common(self, args):
        log.debug('Command: ' + args.subparser_name)
        self.parse_installed_config_file_options(args)
        return

    def parse_uninstall_options(self, args):
        log.debug('Command: ' + args.subparser_name)
        self.parse_installed_config_file_options(args)
        EdgeHostPlatform.uninstall_edge(self.edge_config.home_dir)
        return

    def parse_installed_config_file_options(self, args):
        ins_cfg_file_path = EdgeHostPlatform.get_host_config_file_path()
        if ins_cfg_file_path is None:
            log.error('Runtime has not been configured on this device.\nPlease run \'%s setup\' first.' % (EdgeCLI.prog(),))
            self.exit(1)
        else:
            log.info('Found config file:' + ins_cfg_file_path)
        ip_type = EC.EdgeConfigInputSources.FILE
        parser = EdgeConfigParserFactory.create_parser(ip_type, args)
        self.edge_config = parser.parse(ins_cfg_file_path)
        return

    def parse_and_validate_user_input_config_file(self, args):
        ip_type = EC.EdgeConfigInputSources.FILE
        parser = EdgeConfigParserFactory.create_parser(ip_type, args)
        self.edge_config = parser.parse()
        EdgeHostPlatform.install_edge_by_config_file(args.config_file,
                                                     self.edge_config.home_dir,
                                                     self.edge_config.hostname)
        return

    def present_interactive_menu(self):
        #self.edge_config = EdgeConfigInteractive.present()
        # @todo implementation pending
        print('Feature not yet supported.')
        self.exit(1)

    def parse_and_validate_user_input(self, args):
        ip_type = EC.EdgeConfigInputSources.CLI
        parser = EdgeConfigParserFactory.create_parser(ip_type, args)
        self.edge_config = parser.parse()
        data = self.edge_config.to_json()
        EdgeHostPlatform.install_edge_by_json_data(data,
                                                   self.edge_config.home_dir,
                                                   self.edge_config.hostname)
        return

    def parse_update_options(self, args):
        cmd = args.subparser_name
        if args.image is None:
            log.error('Please specify Edge Agent image with the --image option.')
            raise ValueError('Incorrect input options for command: ' + cmd)
        else:
            self.parse_command_options_common(args)
            if self.edge_config.deployment_config.edge_image == args.image:
                log.info('New Edge Agent image matches existing. Skipping update.')
                self.exit(0)
            else:
                self.edge_config.deployment_config.edge_image = args.image
                EdgeHostPlatform.install_edge_by_json_data(self.edge_config.to_json(),
                                                           self.edge_config.home_dir,
                                                           self.edge_config.hostname)
                log.info('Updated config file with new image: ' + args.image)
                return

    def parse_login_options(self, args):
        cmd = args.subparser_name
        if args.username is None:
            log.error('Please specify username of container registry with the --username option.')
            raise ValueError('Incorrect input options for command: ' + cmd)
        if args.password is None:
            log.error('Please specify password of container registry with the --password option.')
            raise ValueError('Incorrect input options for command: ' + cmd)
        if args.address is None:
            log.error('Please specify address of container registry with the --address option.')
            raise ValueError('Incorrect input options for command: ' + cmd)
        self.parse_command_options_common(args)
        self.edge_config.deployment_config.add_registry(args.address, args.username, args.password)
        EdgeHostPlatform.install_edge_by_json_data(self.edge_config.to_json(),
                                                   self.edge_config.home_dir,
                                                   self.edge_config.hostname)
        log.info('Updated config file with new registry: ' + args.address)
        return

    def parse_setup_options(self, args):
        cmd = args.subparser_name
        log.debug('Command: ' + cmd)

        if args.interactive and args.config_file is not None:
            log.error('--interactive and --config-file options are mutually exclusive.')
            raise ValueError('Incorrect input options for command: ' + cmd)
        elif args.interactive:
            # we are in interactive mode, present menu to user
            try:
                self.present_interactive_menu()
            except ValueError:
                log.error('Error during interactive menu session.')
                raise
        elif args.config_file is not None:
            # we are in config file mode
            try:
                self.parse_and_validate_user_input_config_file(args)
            except ValueError:
                log.error('Error when parsing configuration file')
                raise
        else:
            # we are in manual mode, validate all the supplied args
            try:
                self.parse_and_validate_user_input(args)
            except ValueError as e:
                log.error(e)
                raise

        return

    def execute_command(self):
        log.debug('Executing command \'%s\'' % (self.command,))
        edge_cmd = EdgeCommandFactory.create_command(self.command,
                                                     self.edge_config)
        edge_cmd.execute()
        return
