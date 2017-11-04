import edgectl.edgeconstants as EC
from edgectl.edgeconfigparsercli import EdgeConfigParserCLI
from edgectl.edgeconfigparserfile import EdgeConfigParserFile


class EdgeConfigParserFactory(object):
    @staticmethod
    def create_parser(input_type, args, deployment=None):
        if EC.EdgeConfigInputSources.FILE == input_type:
            return EdgeConfigParserFile(args)
        elif EC.EdgeConfigInputSources.CLI == input_type:
            return EdgeConfigParserCLI(args, deployment)
        else:
            raise NotImplementedError("Unsupported configuration type.")
