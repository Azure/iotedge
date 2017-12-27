from edgectl.config import EdgeConfigInputSources
from edgectl.config import EdgeConstants as EC
from edgectl.parser.edgeconfigparsercli import EdgeConfigParserCLI
from edgectl.parser.edgeconfigparserfile import EdgeConfigParserFile


class EdgeConfigParserFactory(object):
    @staticmethod
    def create_parser(input_type, args, deployment=None):
        if EdgeConfigInputSources.FILE == input_type:
            return EdgeConfigParserFile(args)
        elif EdgeConfigInputSources.CLI == input_type:
            return EdgeConfigParserCLI(args, deployment)
        else:
            raise NotImplementedError("Unsupported configuration type.")
