class EdgeError(Exception):
    """
    A base class from which all other exceptions inherit.

    If you want to catch all errors that might be raised,
    catch this base exception.
    """
    def __init__(self, msg, ex=None):
        if ex:
            msg += ' : {0}'.format(str(ex))
        super(EdgeError, self).__init__(msg)
        self._ex = ex

class EdgeValueError(EdgeError):
    """Basic exception for any invalid data errors"""
    def __init__(self, msg, ex=None):
        super(EdgeValueError, self).__init__(msg, ex)

class EdgeFileAccessError(EdgeError):
    """Basic exception for any file access (read, write) errors"""
    def __init__(self, msg, file_name, ex=None):
        msg += ': {0}'.format(file_name)
        super(EdgeFileAccessError, self).__init__(msg, ex)
        self.file_name = file_name

class EdgeFileParseError(EdgeError):
    """Basic exception for any config file parse errors"""
    def __init__(self, msg, file_name, ex=None):
        msg += ': {0}'.format(file_name)
        super(EdgeFileParseError, self).__init__(msg, ex)
        self.file_name = file_name

class EdgeDeploymentError(EdgeError):
    """Basic exception for any deployment operation"""
    def __init__(self, msg, ex=None):
        super(EdgeDeploymentError, self).__init__(msg, ex)
