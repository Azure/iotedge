class EdgeDeploymentCommand(object):
    EDGE_RUNTIME_STATUS_UNAVAILABLE = 'UNAVAILABLE'
    EDGE_RUNTIME_STATUS_RUNNING = 'RUNNING'
    EDGE_RUNTIME_STATUS_RESTARTING = 'RESTARTING'
    EDGE_RUNTIME_STATUS_STOPPED = 'STOPPED'

    def __init__(self, config_obj):
        self._config_obj = config_obj

    def start(self):
        pass

    def stop(self):
        pass

    def restart(self):
        pass

    def uninstall(self):
        pass

    def setup(self):
        pass

    def status(self):
        pass

    def update(self):
        pass

    def login(self):
        pass

class EdgeCommand(object):
    def __init__(self, obj):
        self._obj = obj

    def execute(self):
        pass

class EdgeSetupCommand(EdgeCommand):
    def execute(self):
        self._obj.setup()

class EdgeStartCommand(EdgeCommand):
    def execute(self):
        self._obj.start()

class EdgeStopCommand(EdgeCommand):
    def execute(self):
        self._obj.stop()

class EdgeRestartCommand(EdgeCommand):
    def execute(self):
        self._obj.restart()

class EdgeStatusCommand(EdgeCommand):
    def execute(self):
        self._obj.status()

class EdgeUninstallCommand(EdgeCommand):
    def execute(self):
        self._obj.uninstall()

class EdgeUpdateCommand(EdgeCommand):
    def execute(self):
        self._obj.update()

class EdgeLoginCommand(EdgeCommand):
    def execute(self):
        self._obj.login()
