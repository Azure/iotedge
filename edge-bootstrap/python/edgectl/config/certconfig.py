""" This module implements functionality to validate and store configuration
    data related to X.509 certificates required to operate the IoT Edge.
"""
import logging as log
from edgectl.config.default import EdgeDefault
from edgectl.config.edgeconstants import EdgeConstants as EC
from edgectl.config.configbase import EdgeConfigBase
import edgectl.errors
from edgectl.utils import EdgeUtils


DEV_CA_OWNER_CERT_FILE_KEY = 'owner_ca_cert_file'
DEV_CA_CERT_FILE_KEY = 'device_ca_cert_file'
DEV_CA_CHAIN_CERT_FILE_KEY = 'device_ca_chain_cert_file'
DEV_CA_PRIVATEKEY_FILE_KEY = 'device_ca_private_key_file'
FORCE_NO_PASS_KEY = 'force_no_passwords'
DEV_CA_PASS_KEY = 'device_ca_passphrase'
DEV_CA_PASS_FILE_KEY = 'device_ca_passphrase_file'
AGT_CA_PASS_KEY = 'agent_ca_passphrase'
AGT_CA_PASS_FILE_KEY = 'agent_ca_passphrase_file'

class EdgeCertConfig(EdgeConfigBase):
    """
        This class implements APIs to validate and store configuration
        data required to generate X.509 certificates.

        Essentially the data collected supports two flows:
        1) Automated (self signed)
        2) Edge as a gateway (pre installed) which uses:
           Owner CA and chain certificate
           Device CA information (cert, private key and passphrase)
           Agent CA (passphrase)
    """
    def __init__(self):
        super(EdgeCertConfig, self).__init__()
        self._security_option = None
        self._passphrase_dict = {
            FORCE_NO_PASS_KEY: False,
            DEV_CA_PASS_KEY: None,
            DEV_CA_PASS_FILE_KEY: None,
            AGT_CA_PASS_KEY: None,
            AGT_CA_PASS_FILE_KEY: None
        }
        self._dca_files_dict = {
            DEV_CA_OWNER_CERT_FILE_KEY: None,
            DEV_CA_CERT_FILE_KEY: None,
            DEV_CA_CHAIN_CERT_FILE_KEY: None,
            DEV_CA_PRIVATEKEY_FILE_KEY: None
        }
        self._self_signed_cert_subject = {}

    def set_options(self, force_no_passwords, subject_dict, **kwargs):
        """
        Validate and set the security options pertaining to Edge
        certificate provisioning

        Args:
            force_no_passwords (bool): Bypass private key password prompts

            subject_dict (dict):
              edgectl.edgeconstants.SUBJECT_COUNTRY_KEY: 2 letter country code
              edgectl.edgeconstants.SUBJECT_STATE_KEY: state
              edgectl.edgeconstants.SUBJECT_LOCALITY_KEY: locality/city
              edgectl.edgeconstants.SUBJECT_ORGANIZATION_KEY: organization
              edgectl.edgeconstants.SUBJECT_ORGANIZATION_UNIT_KEY: organization unit
              edgectl.edgeconstants.SUBJECT_COMMON_NAME_KEY: device CA common name

            kwargs:
              owner_ca_cert_file (str): Path to Owner CA PEM formatted certificate file
              device_ca_cert_file (str): Path to Device CA PEM formatted certificate file
              device_ca_chain_cert_file (str): Path to Device CA Chain PEM formatted cert file
              device_ca_private_key_file (str): Path to Device CA Private Key PEM formatted file
              device_ca_passphrase (str): Passphrase in ascii to read the Device CA private key
              device_ca_passphrase_file (str): Path to a file containing passphrase in ascii
                                               to read the Device CA private key
              agent_ca_passphrase (str): Passphrase in ascii to use when generating
                                         the Edge Agent CA certificate
              agent_ca_passphrase_file (str): Path to a file containing passphrase in ascii
                                              to use when generating the Edge Agent CA certificate

        Raises:
            edgectl.errors.EdgeFileAccessError - Reporting any file access errors
            ValueError - Any input found to be invalid
        """
        # count the number device CA (DCA) file keys set in kwargs
        count = 0
        for key in list(self._dca_files_dict.keys()):
            if key in list(kwargs.keys()):
                if kwargs[key]:
                    count += 1

        # check the kwargs to make sure every arg needed is set
        security_option = None
        if count == len(list(self._dca_files_dict.keys())):
            # all required pre installed data inputs were provided
            security_option = EC.PREINSTALL_KEY
        elif count == 0:
            # no pre installed data inputs were provided generate self signed certs
            security_option = EC.SELFSIGNED_KEY
        else:
            log.error('Incorrect input data provided when' \
                      ' registering Device CA certificate.\n' \
                      'When registering the Device CA certificate,' \
                      ' the following should be provided:\n'\
                      ' - Device CA certificate file\n' \
                      ' - Device CA''s private key file and it''s passphrase (if any)\n' \
                      ' - Owner CA certificate file\n' \
                      ' - Owner CA to Device CA chain certificate file\n')
        is_valid_input = False
        if security_option:
            log.debug('User certificate option: %s', security_option)
            self._security_option = security_option
            self._force_no_passwords = force_no_passwords
            self._merge_with_default_subject_fields(subject_dict)
            if self._security_option == EC.SELFSIGNED_KEY:
                country = self._self_signed_cert_subject[EC.SUBJECT_COUNTRY_KEY]
                if len(country) != 2:
                    msg = 'Invalid certificate country code {0}. ' \
                          'Length should be 2 characters.'.format(country)
                    log.error(msg)
                    raise ValueError(msg)
            else:
                self._set_dca_file(kwargs, DEV_CA_OWNER_CERT_FILE_KEY)
                self._set_dca_file(kwargs, DEV_CA_CERT_FILE_KEY)
                self._set_dca_file(kwargs, DEV_CA_CHAIN_CERT_FILE_KEY)
                self._set_dca_file(kwargs, DEV_CA_PRIVATEKEY_FILE_KEY)
            is_valid_input = self._handle_passphrases(kwargs)
        if is_valid_input is False:
            raise ValueError('Incorrect certificate options provided')

    def use_self_signed_certificates(self):
        """ API that returns a bool indicating whether the IoT Edge certificates
            are to be generated as self signed certificates or are provisioned
            with a device CA certificate.

            Note: This API this is relevant only after calling API set_security_options().

            Returns:
                True if self signed certificates are to be used, False otherwise.

            Raises:
                ValueError if set_security_options() has not yet been called to
                setup the Edge runtime.
        """
        if self._security_option is not None:
            return self._security_option == EC.SELFSIGNED_KEY
        else:
            raise ValueError('Certificate security options not configured.')

    @property
    def certificate_subject_dict(self):
        """Getter for IoT Edge runtime device certificate subject.
           There is no explicit setter for this configuration data as this is
           relevant only for the self signed device CA certificate flow
           and is populated after calling API set_security_options().
        """
        return self._self_signed_cert_subject

    @property
    def owner_ca_cert_file_path(self):
        """Getter for IoT Edge owner CA certificate file path returned as a string."""
        return self._dca_files_dict[DEV_CA_OWNER_CERT_FILE_KEY]

    @property
    def device_ca_cert_file_path(self):
        """Getter for IoT Edge device CA certificate file path
           returned as a string.
        """
        return self._dca_files_dict[DEV_CA_CERT_FILE_KEY]

    @property
    def device_ca_chain_cert_file_path(self):
        """Getter for IoT Edge device CA chain certificate file path
           returned as a string.
        """
        return self._dca_files_dict[DEV_CA_CHAIN_CERT_FILE_KEY]

    @property
    def device_ca_private_key_file_path(self):
        """ Getter for IoT Edge device CA private key file path
            returned as a string.
        """
        return self._dca_files_dict[DEV_CA_PRIVATEKEY_FILE_KEY]

    @property
    def device_ca_passphrase(self):
        """ Getter for IoT Edge device CA private key passphrase
            returned as a string.
        """
        return self._passphrase_dict[DEV_CA_PASS_KEY]

    @property
    def agent_ca_passphrase(self):
        """ Getter for IoT Edge agent CA private key passphrase
            returned as a string.
        """
        return self._passphrase_dict[AGT_CA_PASS_KEY]

    @property
    def device_ca_passphrase_file_path(self):
        """ Getter for IoT Edge device CA private key passphrase file path
            returned as a string.
        """
        return self._passphrase_dict[DEV_CA_PASS_FILE_KEY]

    @property
    def agent_ca_passphrase_file_path(self):
        """ Getter for IoT Edge device CA private key passphrase file path
            returned as a string.
        """
        return self._passphrase_dict[AGT_CA_PASS_FILE_KEY]

    @property
    def force_no_passwords(self):
        """ Getter for whether passphrases are required for private keys
            returned as a bool.
        """
        return self._passphrase_dict[FORCE_NO_PASS_KEY]

    @force_no_passwords.setter
    def _force_no_passwords(self, value):
        """Setter for whether passphrases are required for private keys.

        Args:
            value (bool): True if passphrases are required False otherwise.

        Raises:
            ValueError if value invalid
        """
        if isinstance(value, bool):
            self._passphrase_dict[FORCE_NO_PASS_KEY] = value
        else:
            raise ValueError('Invalid setting for force no passwords: {0}'.format(value))

    def _merge_with_default_subject_fields(self, subj_dict):
        """ To ensure that we have all the fields required to generate a
            X.509 cert, we merge user provided fields with default values.
        """
        default_cert_subject = EdgeDefault.get_certificate_subject_dict()
        self._self_signed_cert_subject = default_cert_subject.copy()
        if subj_dict:
            self._self_signed_cert_subject.update(subj_dict)
        # country code should be upper cased
        country = self._self_signed_cert_subject[EC.SUBJECT_COUNTRY_KEY]
        self._self_signed_cert_subject[EC.SUBJECT_COUNTRY_KEY] = country.upper()

    @staticmethod
    def _get_ca_passphrase(kwargs, ca_type, pass_key, pass_file_key):
        args_list = list(kwargs.keys())
        ca_passphrase = None
        if pass_key in list(args_list) and kwargs[pass_key]:
            ca_passphrase = kwargs[pass_key]

        ca_passphrase_file = None
        if pass_file_key in list(args_list) and kwargs[pass_file_key]:
            ca_passphrase_file = kwargs[pass_file_key]

        is_valid = True
        if ca_passphrase and ca_passphrase_file:
            is_valid = False
            log.error('Passphrase and passphrase file both cannot be set ' \
                      'for %s private key', ca_type)
        elif ca_passphrase_file:
            try:
                with open(ca_passphrase_file, 'r') as ip_file:
                    ca_passphrase = ip_file.read().rstrip()
            except IOError as ex_os:
                msg = 'Error reading file: {0}. Errno: {1}, Error {2}'.format(ca_passphrase_file,
                                                                              ex_os.errno,
                                                                              ex_os.strerror)
                log.error(msg)
                raise edgectl.errors.EdgeFileAccessError(msg, ca_passphrase_file)

        return (is_valid, ca_passphrase, ca_passphrase_file)

    def _handle_passphrases(self, kwargs):
        is_valid_input = False

        (is_dca_pass_valid, dca_passphrase, dca_passphrase_file) = \
            self._get_ca_passphrase(kwargs, 'Device CA', DEV_CA_PASS_KEY, DEV_CA_PASS_FILE_KEY)
        (is_agt_pass_valid, agt_passphrase, agt_passphrase_file) = \
            self._get_ca_passphrase(kwargs, 'Agent CA', AGT_CA_PASS_KEY, AGT_CA_PASS_FILE_KEY)

        if is_dca_pass_valid is True and is_agt_pass_valid is True:
            if agt_passphrase and self.force_no_passwords is True:
                log.error('Inconsistent password options. Force no passwords ' \
                          'was specified and an Agent CA passphrase was provided.')
            elif dca_passphrase and self.force_no_passwords is True and \
                    self._security_option == EC.SELFSIGNED_KEY:
                log.error('Inconsistent password options. Force no passwords ' \
                        'was specified and a Device CA passphrase was provided.')
            else:
                self._passphrase_dict[DEV_CA_PASS_KEY] = dca_passphrase
                self._passphrase_dict[DEV_CA_PASS_FILE_KEY] = dca_passphrase_file
                self._passphrase_dict[AGT_CA_PASS_KEY] = agt_passphrase
                self._passphrase_dict[AGT_CA_PASS_FILE_KEY] = agt_passphrase_file
                is_valid_input = True

        return is_valid_input

    def _set_dca_file(self, kwargs, file_key):
        """Helper method to store one of the many device CA certificate and
           private key files required to operate the Edge as a gateway.

        Args:
            kwargs (dict): User supplied KW args
            file_key (str): Key to retrieve and store the file

        Raises:
            ValueError if retrieved file path is None or if the file does not exist.
        """
        file_path = kwargs[file_key]
        if EdgeUtils.check_if_file_exists(file_path):
            self._dca_files_dict[file_key] = file_path
        else:
            raise ValueError('Invalid {0} file: {1}'.format(file_key, file_path))

    def _cert_subject_to_str(self):
        result = ''
        output_keys = [EC.SUBJECT_COUNTRY_KEY, EC.SUBJECT_STATE_KEY,
                       EC.SUBJECT_LOCALITY_KEY, EC.SUBJECT_ORGANIZATION_KEY,
                       EC.SUBJECT_ORGANIZATION_UNIT_KEY,
                       EC.SUBJECT_COMMON_NAME_KEY]
        input_keys = list(self._self_signed_cert_subject.keys())
        new_line_idx = 0
        for output_key in output_keys:
            if output_key in input_keys:
                if new_line_idx != 0:
                    result += ', '
                else:
                    result += '\n\t\t\t'
                new_line_idx = (new_line_idx + 1) % 3
                result += output_key + ': ' + self._self_signed_cert_subject[output_key]
        return result

    def to_dict(self):
        certs_dict = {}
        if self._security_option is not None:
            certs_dict[EC.CERTS_SUBJECT_KEY] = self._self_signed_cert_subject
            if self.use_self_signed_certificates() is True:
                # handle self signed cert options
                security_opt_selfsigned = {}
                security_opt_selfsigned[EC.FORCENOPASSWD_KEY] = \
                    self.force_no_passwords
                security_opt_selfsigned[EC.DEVICE_CA_PASSPHRASE_FILE_KEY] = \
                    self.device_ca_passphrase_file_path
                security_opt_selfsigned[EC.AGENT_CA_PASSPHRASE_FILE_KEY] = \
                    self.agent_ca_passphrase_file_path
                certs_dict[EC.CERTS_OPTION_KEY] = EC.SELFSIGNED_KEY
                certs_dict[EC.SELFSIGNED_KEY] = security_opt_selfsigned
            elif self._security_option == EC.PREINSTALL_KEY:
                # pre installed cert options
                security_opt_preinstalled = {}
                security_opt_preinstalled[EC.PREINSTALL_OWNER_CA_CERT_KEY] = \
                    self.owner_ca_cert_file_path
                security_opt_preinstalled[EC.PREINSTALL_DEVICE_CERT_KEY] = \
                    self.device_ca_cert_file_path
                security_opt_preinstalled[EC.PREINSTALL_DEVICE_CHAINCERT_KEY] = \
                    self.device_ca_chain_cert_file_path
                security_opt_preinstalled[EC.PREINSTALL_DEVICE_PRIVKEY_KEY] = \
                    self.device_ca_private_key_file_path
                security_opt_preinstalled[EC.DEVICE_CA_PASSPHRASE_FILE_KEY] = \
                    self.device_ca_passphrase_file_path
                security_opt_preinstalled[EC.AGENT_CA_PASSPHRASE_FILE_KEY] = \
                    self.agent_ca_passphrase_file_path
                security_opt_preinstalled[EC.FORCENOPASSWD_KEY] = \
                    self.force_no_passwords
                certs_dict[EC.CERTS_OPTION_KEY] = EC.PREINSTALL_KEY
                certs_dict[EC.PREINSTALL_KEY] = security_opt_preinstalled

        security_dict = {EC.CERTS_KEY: certs_dict}
        return security_dict

    def __str__(self):
        result = ''
        if self._security_option is not None:
            result += 'Security Option:\t' + self._security_option + '\n'
            result += 'Force No Passwords:\t' + str(self.force_no_passwords) + '\n'
            if self.use_self_signed_certificates() is False:
                result += 'Owner CA Cert File:\t\n'
                result += '\t\t\t' + str(self.owner_ca_cert_file_path) + '\n'
                result += 'Device CA Cert File:\t\n'
                result += '\t\t\t' + str(self.device_ca_cert_file_path) + '\n'
                result += 'Device CA Chain Cert File:\t\n'
                result += '\t\t\t' + str(self.device_ca_chain_cert_file_path) + '\n'
                result += 'Device CA Private Key File:\t\n'
                result += '\t\t\t' + str(self.device_ca_private_key_file_path) + '\n'
            else:
                result += 'Certificate Subject:\t' + self._cert_subject_to_str() + '\n'
            if self.device_ca_passphrase_file_path is not None:
                result += 'Device CA Passphrase File:\n'
                result += '\t\t\t' + str(self.device_ca_passphrase_file_path) + '\n'
            if self.agent_ca_passphrase_file_path is not None:
                result += 'Agent CA Passphrase File:\n'
                result += '\t\t\t' + str(self.agent_ca_passphrase_file_path) + '\n'

        return result
