""" Module implements utility class EdgeCertUtil for generating a X.509 certificate chain"""
import logging
import os
from datetime import datetime
from shutil import copy2
from OpenSSL import crypto
from edgectl.config import EdgeConstants as EC
import edgectl.errors
from edgectl.utils.edgeutils import EdgeUtils


class EdgeCertUtil(object):
    """ Class EdgeCertUtil implements APIs for generating X.509 certificate chains.
        Clients are expected to begin either by calling create_root_ca_cert() or
        set_root_ca_cert() to establish a root CA certificate in the chain.
        Thereafter, clients can call API create_intermediate_ca_cert() to create
        any number of intermediate CA certs. To terminate a chain clients can
        call create_server_cert(). To export the certificate chain clients should
        call APIs export_cert_artifacts_to_dir() export_pfx_cert().
    """
    TYPE_RSA = 0
    MIN_VALIDITY_DAYS = 1
    MAX_VALIDITY_DAYS = 1095 #3 years
    MIN_PASSPHRASE_LENGTH = 4
    MAX_PASSPHRASE_LENGTH = 1023
    CA_KEY_LEN = 4096
    CA_INT_KEY_LEN = 4096
    SERVER_KEY_LEN = 2048
    MIN_COMMON_NAME_LEN = 1
    MAX_COMMON_NAME_LEN = 64
    DIGEST = 'sha256'
    _type_dict = {TYPE_RSA: crypto.TYPE_RSA}
    _subject_validation_dict = {
        EC.SUBJECT_COUNTRY_KEY: {'MIN': 2, 'MAX': 2},
        EC.SUBJECT_STATE_KEY: {'MIN': 0, 'MAX': 128},
        EC.SUBJECT_LOCALITY_KEY: {'MIN': 0, 'MAX': 128},
        EC.SUBJECT_ORGANIZATION_KEY: {'MIN': 0, 'MAX': 64},
        EC.SUBJECT_ORGANIZATION_UNIT_KEY: {'MIN': 0, 'MAX': 64},
        EC.SUBJECT_COMMON_NAME_KEY: {'MIN': MIN_COMMON_NAME_LEN, 'MAX': MAX_COMMON_NAME_LEN},
    }

    def __init__(self, serial_num=1000):
        self._cert_chain = {}
        self._serial_number = serial_num

    @staticmethod
    def is_valid_certificate_subject(subject_dict):
        """
        Utility API to validate if the certificate subject fields are valid.
        Validates if all the required keys listed below are present and have valid
        values per the description.

        Args:
            subject_dict (dict): Certificate subject dict with both key and values as strings
                edgectl.edgeconstants.SUBJECT_COUNTRY_KEY: Country code (2 chars)
                edgectl.edgeconstants.SUBJECT_STATE_KEY: State (128 chars)
                edgectl.edgeconstants.SUBJECT_LOCALITY_KEY: Locality/city (128 chars)
                edgectl.edgeconstants.SUBJECT_ORGANIZATION_KEY: organization (64 chars)
                edgectl.edgeconstants.SUBJECT_ORGANIZATION_UNIT_KEY: organization unit (64 chars)
                edgectl.edgeconstants.SUBJECT_COMMON_NAME_KEY: device CA common name (64 chars)

        Returns:
            True if the subject field is valid, False otherwise.
        """
        result = True
        for key in list(EdgeCertUtil._subject_validation_dict.keys()):
            try:
                field = subject_dict[key]
                if field is not None:
                    length_field = len(field)
                    min_len = EdgeCertUtil._subject_validation_dict[key]['MIN']
                    max_len = EdgeCertUtil._subject_validation_dict[key]['MAX']
                    if length_field < min_len or length_field > max_len:
                        logging.error('Length of X.509 cert subject field: %s is invalid', key)
                        result = False
                else:
                    logging.error('Value for field: %s cannot be None', key)
                    result = False
            except KeyError:
                logging.error('Missing key in X.509 certificate subject: %s', key)
                result = False

            if result is False:
                break
        return result

    def create_root_ca_cert(self, id_str, **kwargs):
        """
        API to create the root certificate in the certificate chain. This implies that the
        CA certificate will be self signed.

        Args:
            id_str (str): A user defined unique id string to identify the certificate

            kwargs:
                validity_days_from_now (int): Number of days for certificate validity starting
                                              from the time the API was invoked. Optional,
                                              if validity is not provided default is 365 days.
                                              Validity days min: EdgeCertUtil.MIN_VALIDITY,
                                              max: EdgeCertUtil.MAX_VALIDITY.

                subject_dict (dict): Certificate subject dict set per specifications of API
                                     validate_certificate_subject(). Required.

                passphrase (str): Private key passphrase. Optional.
                                  Passphrase length min: EdgeCertUtil.MIN_PASSPHRASE_LENGTH,
                                  max: EdgeCertUtil.MAX_PASSPHRASE_LENGTH.

        Raises:
            edgectl.errors.EdgeValueError - Any input found to be invalid
        """
        if id_str in list(self._cert_chain.keys()):
            msg = 'Duplicate root CA certificate ID: {0}'.format(id_str)
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

        validity_days_from_now = _get_kwargs_validity('validity_days_from_now',
                                                      self.MIN_VALIDITY_DAYS,
                                                      self.MAX_VALIDITY_DAYS, **kwargs)

        subj_dict = None
        if 'subject_dict' in kwargs:
            subj_dict = kwargs['subject_dict']
            if self.is_valid_certificate_subject(subj_dict) is False:
                msg = 'Certificate subject dictionary is invalid.'
                logging.error(msg)
                raise edgectl.errors.EdgeValueError(msg)
        else:
            msg = 'Certificate subject dictionary is required.'
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

        passphrase = _get_kwargs_passphrase('passphrase', self.MIN_PASSPHRASE_LENGTH,
                                            self.MAX_PASSPHRASE_LENGTH, **kwargs)

        key_obj = self._create_key_pair(self.TYPE_RSA, self.CA_KEY_LEN)
        csr_obj = self._create_csr(key_obj,
                                   C=subj_dict[EC.SUBJECT_COUNTRY_KEY],
                                   ST=subj_dict[EC.SUBJECT_STATE_KEY],
                                   L=subj_dict[EC.SUBJECT_LOCALITY_KEY],
                                   O=subj_dict[EC.SUBJECT_ORGANIZATION_KEY],
                                   OU=subj_dict[EC.SUBJECT_ORGANIZATION_KEY],
                                   CN=subj_dict[EC.SUBJECT_COMMON_NAME_KEY])

        validity_secs_from_now = validity_days_from_now * 24 * 60 * 60
        cert_obj = self._create_ca_cert(csr_obj,
                                        csr_obj,
                                        key_obj,
                                        self._serial_number,
                                        (0, validity_secs_from_now),
                                        False)
        self._serial_number += 1
        cert_dict = {}
        cert_dict['key_pair'] = key_obj
        cert_dict['csr'] = csr_obj
        cert_dict['cert'] = cert_obj
        cert_dict['issuer_id'] = id_str
        cert_dict['passphrase'] = passphrase
        self._cert_chain[id_str] = cert_dict
        return

    def set_ca_cert(self, id_str, **kwargs):
        """
        API to set a CA certificate in the certificate chain. This certificate may be
        an intermediate CA certificate or a root CA certificate.

        Args:
            id_str (str): A user defined unique id string to identify the certificate

            kwargs:
                ca_cert_file_path (str): File path to the CA certificate

                ca_private_key_file_path (str): File path to the CA certificate's private key

                ca_root_cert_file_path (str): File path to the CA certificate's root
                                              certificate if any. If this is a self
                                              signed root certificate set this to the same
                                              value as kwarg 'ca_cert_file_path'.

                ca_root_chain_cert_file_path (str): File path to the CA certificate's root chain
                                                    certificate if any. If this is a self
                                                    signed root certificate set this to the same
                                                    value as kwarg 'ca_cert_file_path'.

                passphrase (str): Private key passphrase. Optional.
                                  Passphrase length min: EdgeCertUtil.MIN_PASSPHRASE_LENGTH,
                                  max: EdgeCertUtil.MAX_PASSPHRASE_LENGTH.

        Raises:
            edgectl.errors.EdgeValueError - Any input found to be invalid
            edgectl.errors.EdgeFileAccessError - If any of the files cannot be read or
                                                 are in an invalid format.
        """
        if id_str in list(self._cert_chain.keys()):
            msg = 'Duplicate CA certificate ID: {0}'.format(id_str)
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

        ca_cert_file_path = _get_kwargs_files('ca_cert_file_path', 'CA certificate', **kwargs)

        ca_private_key_file_path = _get_kwargs_files('ca_private_key_file_path',
                                                     'CA private key', **kwargs)

        ca_root_cert_file_path = _get_kwargs_files('ca_root_cert_file_path',
                                                   'CA''s root certificate', **kwargs)

        ca_root_chain_cert_file_path = _get_kwargs_files('ca_root_chain_cert_file_path',
                                                         'CA''s chain certificate', **kwargs)

        passphrase = _get_kwargs_passphrase('passphrase', self.MIN_PASSPHRASE_LENGTH,
                                            self.MAX_PASSPHRASE_LENGTH, **kwargs)

        logging.debug('Setting Root CA for id: %s\n' \
                      '  CA Cert File: %s\n' \
                      '  CA Root Cert File: %s\n' \
                      '  CA Root Chain Cert File: %s\n' \
                      '  CA Private Key File: %s',
                      id_str, ca_cert_file_path,
                      ca_root_cert_file_path, ca_root_chain_cert_file_path,
                      ca_private_key_file_path)
        try:
            # read the CA private key
            with open(ca_private_key_file_path, 'rb') as key_file:
                pk_data = key_file.read()
            ca_key_obj = crypto.load_privatekey(crypto.FILETYPE_PEM,
                                                pk_data,
                                                passphrase=passphrase)
            ca_key_obj.check()
        except crypto.Error as ex_crypto:
            msg = 'Cryptographic error when reading private key file: {0}.' \
                  ' Error: {1}'.format(ca_private_key_file_path, str(ex_crypto))
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)
        except TypeError as ex_type:
            logging.error('%s', str(ex_type))
            msg = 'Unsupported private key type. Currently RSA is only supported.'
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)
        except IOError as ex:
            msg = 'Could not read private key file: {0}.' \
                  ' Errno: {1} Error: {2}'.format(ca_private_key_file_path,
                                                  str(ex.errno), ex.strerror)
            logging.error(msg)
            raise edgectl.errors.EdgeFileAccessError(msg, ca_private_key_file_path)

        try:
            # read the CA certificate
            with open(ca_cert_file_path, 'rb') as cert_file:
                cert_data = cert_file.read()
            ca_cert_obj = crypto.load_certificate(crypto.FILETYPE_PEM, cert_data)

            if ca_cert_obj.has_expired():
                msg = 'Expired CA certificate provided: {0}'.format(ca_cert_file_path)
                logging.error(msg)
                raise edgectl.errors.EdgeValueError(msg)

            cert_dict = {}
            cert_dict['key_pair'] = ca_key_obj
            cert_dict['cert'] = ca_cert_obj
            cert_dict['issuer_id'] = id_str
            cert_dict['key_file'] = ca_private_key_file_path
            cert_dict['ca_chain'] = ca_root_chain_cert_file_path
            cert_dict['ca_root'] = ca_root_cert_file_path
            self._cert_chain[id_str] = cert_dict
        except crypto.Error as ex_crypto:
            msg = 'Crypto Error: {0}'.format(str(ex_crypto))
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)
        except IOError as ex:
            msg = 'Could not read certificate file: {0}.' \
                  ' Errno: {1} Error: {2}'.format(ca_cert_file_path,
                                                  str(ex.errno), ex.strerror)
            logging.error(msg)
            raise edgectl.errors.EdgeFileAccessError(msg, ca_cert_file_path)

    @staticmethod
    def _get_maximum_validity_days(not_after_ts_asn1, validity_days_from_now):
        ''' Returns the least number of days between:
            - now() and the certificate expiration timestamp and
            - requested certificate expiration time expressed in days from now()
        '''
        result = 0
        try:
            expiration_date = datetime.strptime(not_after_ts_asn1.decode('utf-8'), "%Y%m%d%H%M%SZ")
            expires_in = expiration_date - datetime.now()
            if expires_in.days > 0:
                result = min(expires_in.days, validity_days_from_now)
            logging.debug('Max validity days: %s,' \
                          ' Certificate expiration timestamp: %s,' \
                          ' Certificate expiration days: %s,' \
                          ' Requested expiration days: %s',
                          str(result), not_after_ts_asn1, str(expires_in.days),
                          str(validity_days_from_now))
            return result
        except:
            msg = 'Certificate date format incompatible {0}'.format(not_after_ts_asn1)
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

    def create_intermediate_ca_cert(self, id_str, issuer_id_str, **kwargs):
        """
        API to create an intermediate CA certificate issued by another CA in the certificate chain.

        Args:
            id_str (str): A user defined unique id string to identify the certificate
            issuer_id_str (str): A unique id string to identify the issuing CA

            kwargs:
                validity_days_from_now (int): Number of days for certificate validity starting
                                              from the time the API was invoked. Optional,
                                              if validity is not provided default is 365 days.
                                              Validity days min: EdgeCertUtil.MIN_VALIDITY,
                                              max: EdgeCertUtil.MAX_VALIDITY.

                common_name (str): Common name for the CA certificate. Required.
                                   Common name length min: EdgeCertUtil.MIN_COMMON_NAME_LEN,
                                   max: EdgeCertUtil.MAX_COMMON_NAME_LEN.

                set_terminal_ca (bool): If set to True, it sets path len to zero which
                                        implies that this CA cannot issue other CA certificates.
                                        This CA can however issue non CA certificates. Optional.
                                        Default is True.

                passphrase (str): Private key passphrase. Optional.
                                  Passphrase length min: EdgeCertUtil.MIN_PASSPHRASE_LENGTH,
                                  max: EdgeCertUtil.MAX_PASSPHRASE_LENGTH.
        Raises:
            edgectl.errors.EdgeValueError - Any input found to be invalid
            edgectl.errors.EdgeFileAccessError - If any of the files cannot be read or
                                                 are in an invalid format.
        """
        if id_str in list(self._cert_chain.keys()):
            msg = 'Duplicate intermediate CA certificate ID: {0}'.format(id_str)
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

        if issuer_id_str not in list(self._cert_chain.keys()):
            msg = 'Invalid issuer certificate ID: {0}'.format(issuer_id_str)
            raise edgectl.errors.EdgeValueError(msg)

        validity_days_from_now = _get_kwargs_validity('validity_days_from_now',
                                                      self.MIN_VALIDITY_DAYS,
                                                      self.MAX_VALIDITY_DAYS, **kwargs)

        passphrase = _get_kwargs_passphrase('passphrase', self.MIN_PASSPHRASE_LENGTH,
                                            self.MAX_PASSPHRASE_LENGTH, **kwargs)

        min_length = self._subject_validation_dict[EC.SUBJECT_COMMON_NAME_KEY]['MIN']
        max_length = self._subject_validation_dict[EC.SUBJECT_COMMON_NAME_KEY]['MAX']
        common_name = _get_kwargs_string('common_name', min_length, max_length, **kwargs)
        if common_name is None:
            msg = 'Invalid common name: {0}'.format(common_name)
            raise edgectl.errors.EdgeValueError(msg)

        set_terminal_ca = True
        if 'set_terminal_ca' in kwargs:
            set_terminal_ca = kwargs['set_terminal_ca']

        try:
            issuer_cert_dict = self._cert_chain[issuer_id_str]
            issuer_cert = issuer_cert_dict['cert']

            not_after_ts = issuer_cert.get_notAfter()
            valid_days = self._get_maximum_validity_days(not_after_ts,
                                                         validity_days_from_now)

            issuer_key = issuer_cert_dict['key_pair']
            key_obj = self._create_key_pair(self.TYPE_RSA, self.CA_KEY_LEN)
            csr_obj = self._create_csr(key_obj,
                                       C=issuer_cert.get_subject().countryName,
                                       ST=issuer_cert.get_subject().stateOrProvinceName,
                                       L=issuer_cert.get_subject().localityName,
                                       O=issuer_cert.get_subject().organizationName,
                                       OU=issuer_cert.get_subject().organizationalUnitName,
                                       CN=common_name)

            validity_secs_from_now = valid_days * 24 * 60 * 60
            cert_obj = self._create_ca_cert(csr_obj,
                                            issuer_cert,
                                            issuer_key,
                                            self._serial_number,
                                            (0, validity_secs_from_now),
                                            set_terminal_ca)
            self._serial_number += 1
            cert_dict = {}
            cert_dict['key_pair'] = key_obj
            cert_dict['csr'] = csr_obj
            cert_dict['cert'] = cert_obj
            cert_dict['issuer_id'] = issuer_id_str
            cert_dict['passphrase'] = passphrase
            self._cert_chain[id_str] = cert_dict
        except edgectl.errors.EdgeValueError:
            msg = 'Could not create intermediate certificate for {0}'.format(id_str)
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

    def create_server_cert(self, id_str, issuer_id_str, **kwargs):
        """
        API to create server certificate issued by another CA in the certificate chain.

        Args:
            id_str (str): A user defined unique id string to identify the certificate
            issuer_id_str (str): A unique id string to identify the issuing CA

            kwargs:
                validity_days_from_now (int): Number of days for certificate validity starting
                                              from the time the API was invoked. Optional,
                                              if validity is not provided default is 365 days.
                                              Validity days min: EdgeCertUtil.MIN_VALIDITY,
                                              max: EdgeCertUtil.MAX_VALIDITY.

                hostname (str): Server hostname which will be used as the certificate common name.
                                Required. Hostname length min: EdgeCertUtil.MIN_COMMON_NAME_LEN,
                                max: EdgeCertUtil.MAX_COMMON_NAME_LEN.

                passphrase (str): Private key passphrase. Optional.
                                  Passphrase length min: EdgeCertUtil.MIN_PASSPHRASE_LENGTH,
                                  max: EdgeCertUtil.MAX_PASSPHRASE_LENGTH.

        Raises:
            edgectl.errors.EdgeValueError - Any input found to be invalid
        """
        if id_str in list(self._cert_chain.keys()):
            msg = 'Duplicate intermediate CA certificate ID: {0}'.format(id_str)
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

        if issuer_id_str not in list(self._cert_chain.keys()):
            msg = 'Invalid issuer certificate ID: {0}'.format(issuer_id_str)
            raise edgectl.errors.EdgeValueError(msg)

        validity_days_from_now = _get_kwargs_validity('validity_days_from_now',
                                                      self.MIN_VALIDITY_DAYS,
                                                      self.MAX_VALIDITY_DAYS, **kwargs)

        passphrase = _get_kwargs_passphrase('passphrase', self.MIN_PASSPHRASE_LENGTH,
                                            self.MAX_PASSPHRASE_LENGTH, **kwargs)


        min_length = self._subject_validation_dict[EC.SUBJECT_COMMON_NAME_KEY]['MIN']
        max_length = self._subject_validation_dict[EC.SUBJECT_COMMON_NAME_KEY]['MAX']
        hostname = _get_kwargs_string('hostname', min_length, max_length, **kwargs)
        if hostname is None:
            msg = 'Invalid hostname: {0}'.format(hostname)
            raise edgectl.errors.EdgeValueError(msg)

        try:
            issuer_cert_dict = self._cert_chain[issuer_id_str]
            issuer_cert = issuer_cert_dict['cert']
            issuer_key = issuer_cert_dict['key_pair']
            key_obj = self._create_key_pair(self.TYPE_RSA, self.SERVER_KEY_LEN)
            csr_obj = self._create_csr(key_obj,
                                       C=issuer_cert.get_subject().countryName,
                                       ST=issuer_cert.get_subject().stateOrProvinceName,
                                       L=issuer_cert.get_subject().localityName,
                                       O=issuer_cert.get_subject().organizationName,
                                       OU=issuer_cert.get_subject().organizationalUnitName,
                                       CN=hostname)
            not_after_ts = issuer_cert.get_notAfter()
            valid_days = self._get_maximum_validity_days(not_after_ts,
                                                         validity_days_from_now)
            validity_secs_from_now = valid_days * 24 * 60 * 60
            cert_obj = self._create_server_cert(csr_obj,
                                                issuer_cert,
                                                issuer_key,
                                                self._serial_number,
                                                (0, validity_secs_from_now))
            self._serial_number += 1
            cert_dict = {}
            cert_dict['key_pair'] = key_obj
            cert_dict['csr'] = csr_obj
            cert_dict['cert'] = cert_obj
            cert_dict['issuer_id'] = issuer_id_str
            cert_dict['passphrase'] = passphrase
            self._cert_chain[id_str] = cert_dict
        except edgectl.errors.EdgeValueError:
            msg = 'Could not create server certificate for {0}'.format(id_str)
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

    def export_cert_artifacts_to_dir(self, id_str, dir_path):
        """
        API to generate certificate, private key files along with any root and chain certificates
        for certificate identified by id_str

        Args:
            id_str (str): A user defined unique id string to identify the certificate
            dir_path (str): Valid directory path where the certificates are to be exported.

        Raises:
            edgectl.errors.EdgeValueError - Any input found to be invalid
            edgectl.errors.EdgeFileAccessError
        """
        if EdgeUtils.check_if_directory_exists(dir_path) is False:
            msg = 'Invalid export directory {0}'.format(dir_path)
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

        if id_str not in list(self._cert_chain.keys()):
            msg = 'Certificate not in chain. ID: {0}'.format(id_str)
            raise edgectl.errors.EdgeValueError(msg)

        cert_dict = self._cert_chain[id_str]
        prefix = id_str
        try:
            path = os.path.realpath(dir_path)
            path = os.path.join(path, prefix)
            logging.debug('Deleting dir: %s', path)
            EdgeUtils.delete_dir(path)
            logging.debug('Creating dir: %s', path)
            EdgeUtils.mkdir_if_needed(path)
            priv_dir = os.path.join(path, 'private')
            logging.debug('Creating dir: %s', priv_dir)
            EdgeUtils.mkdir_if_needed(priv_dir)
            os.chmod(priv_dir, 0o700)
            cert_dir = os.path.join(path, 'cert')
            logging.debug('Creating dir: %s', cert_dir)
            EdgeUtils.mkdir_if_needed(cert_dir)

            # export the private key
            priv_key_file_name = prefix + '.key.pem'
            priv_key_file = os.path.join(priv_dir, priv_key_file_name)
            if 'key_file' in cert_dict:
                key_file_path = cert_dict['key_file']
                logging.debug('Copying Private Key File %s To %s', key_file_path, priv_key_file)
                copy2(key_file_path, priv_key_file)
            else:
                key_obj = cert_dict['key_pair']
                key_passphrase = cert_dict['passphrase']
                passphrase = None
                if key_passphrase and key_passphrase != '':
                    passphrase = key_passphrase.encode('utf-8')
                logging.debug('Exporting Private Key File: %s', priv_key_file)
                with open(priv_key_file, 'w') as ip_file:
                    cipher = None
                    if passphrase:
                        cipher = 'aes256'
                    ip_file.write(crypto.dump_privatekey(crypto.FILETYPE_PEM,
                                                         key_obj,
                                                         cipher=cipher,
                                                         passphrase=passphrase).decode('utf-8'))

            # export the cert
            cert_obj = cert_dict['cert']
            cert_file_name = prefix + '.cert.pem'
            cert_file = os.path.join(cert_dir, cert_file_name)
            current_cert_file_path = cert_file
            logging.debug('Exporting Certificate File: %s', cert_file)
            with open(cert_file, 'w') as ip_file:
                ip_file.write(crypto.dump_certificate(crypto.FILETYPE_PEM,
                                                      cert_obj).decode('utf-8'))

            # export any chain certs
            if 'ca_chain' in list(cert_dict.keys()):
                src_chain_cert_file = cert_dict['ca_chain']
                cert_file_name = prefix + '-chain.cert.pem'
                cert_file = os.path.join(cert_dir, cert_file_name)
                logging.debug('Copying CA Chain Certificate File %s To %s',
                              src_chain_cert_file, cert_file)
                copy2(src_chain_cert_file, cert_file)

            # check if this is the root cert in the chain, i.e. issuer is itself
            if cert_dict['issuer_id'] == id_str:
                cert_file_name = prefix + '-root.cert.pem'
                cert_file = os.path.join(cert_dir, cert_file_name)
                # export the ca cert's root ca (parent)
                if 'ca_root' in list(cert_dict.keys()):
                    src_root_cert_file = cert_dict['ca_root']
                else:
                    src_root_cert_file = current_cert_file_path
                logging.debug('Copying CA Root Certificate File %s To %s',
                              src_root_cert_file, cert_file)
                copy2(src_root_cert_file, cert_file)
        except IOError as ex:
            msg = 'IO Error when exporting certs for ID: {0}.\n' \
                  ' Error seen when copying/exporting file {1}.' \
                  ' Errno: {2} Error: {3}'.format(id_str, ex.filename, str(ex.errno), ex.strerror)
            logging.error(msg)
            raise edgectl.errors.EdgeFileAccessError(msg, path)

    def export_pfx_cert(self, id_str, dir_path):
        """
        API to export a certificate in PFX format to given directory path

        Args:
            id_str (str): A user defined unique id string to identify the certificate
            dir_path (str): Valid directory path where the certificates are to be exported.

        Raises:
            edgectl.errors.EdgeValueError - Any input found to be invalid
            edgectl.errors.EdgeFileAccessError
        """
        if id_str not in self._cert_chain:
            msg = 'Invalid cert ID: {0}'.format(id_str)
            raise edgectl.errors.EdgeValueError(msg)

        try:
            cert_dict = self._cert_chain[id_str]
            cert_obj = cert_dict['cert']
            key_obj = cert_dict['key_pair']
            pfx = crypto.PKCS12()
            pfx.set_privatekey(key_obj)
            pfx.set_certificate(cert_obj)
            pfx_data = pfx.export('')
            prefix = id_str
            path = os.path.realpath(dir_path)
            path = os.path.join(path, prefix)
            cert_dir = os.path.join(path, 'cert')
            pfx_output_file_name = os.path.join(cert_dir, prefix + '.cert.pfx')
            logging.debug('Exporting PFX Certificate File: %s', pfx_output_file_name)
            with open(pfx_output_file_name, 'wb') as pfx_file:
                pfx_file.write(pfx_data)
        except IOError as ex:
            msg = 'IO Error when exporting PFX cert ID: {0}.' \
                  ' Errno: {1} Error: {2}'.format(id_str, str(ex.errno), ex.strerror)
            logging.error(msg)
            raise edgectl.errors.EdgeFileAccessError(msg, pfx_output_file_name)

    @staticmethod
    def _create_key_pair(private_key_type, key_bit_len):
        key_pair = crypto.PKey()
        key_pair.generate_key(EdgeCertUtil._type_dict[private_key_type], key_bit_len)
        return key_pair

    @staticmethod
    def _create_csr(key_pair, **kwargs):
        csr = crypto.X509Req()
        subj = csr.get_subject()
        for key, value in list(kwargs.items()):
            if value:
                setattr(subj, key, value)
        csr.set_pubkey(key_pair)
        csr.sign(key_pair, EdgeCertUtil.DIGEST)
        return csr

    @staticmethod
    def _create_cert_common(csr,
                            issuer_cert,
                            serial_num,
                            validity_period):
        not_before, not_after = validity_period
        cert = crypto.X509()
        cert.set_serial_number(serial_num)
        cert.gmtime_adj_notBefore(not_before)
        cert.gmtime_adj_notAfter(not_after)
        cert.set_issuer(issuer_cert.get_subject())
        cert.set_subject(csr.get_subject())
        cert.set_pubkey(csr.get_pubkey())
        return cert


    @staticmethod
    def _create_ca_cert(csr,
                        issuer_cert,
                        issuer_key_pair,
                        serial_num,
                        validity_period,
                        path_len_zero):
        cert = EdgeCertUtil._create_cert_common(csr,
                                                issuer_cert,
                                                serial_num,
                                                validity_period)
        val = b'CA:TRUE'
        if path_len_zero:
            val += b', pathlen:0'
        extensions = []
        extensions.append(crypto.X509Extension(b'basicConstraints',
                                               critical=True, value=val))
        cert.add_extensions(extensions)
        cert.sign(issuer_key_pair, EdgeCertUtil.DIGEST)
        return cert

    @staticmethod
    def _create_server_cert(csr,
                            issuer_cert,
                            issuer_key_pair,
                            serial_num,
                            validity_period):
        cert = EdgeCertUtil._create_cert_common(csr,
                                                issuer_cert,
                                                serial_num,
                                                validity_period)

        extensions = []
        extensions.append(crypto.X509Extension(b'basicConstraints',
                                               critical=False,
                                               value=b'CA:FALSE'))
        cert.add_extensions(extensions)
        cert.sign(issuer_key_pair, EdgeCertUtil.DIGEST)

        return cert

    def chain_ca_certs(self, output_prefix, prefixes, certs_dir):
        """
        API to export a certificate and its chain up all certificates in argument prefixes.

        Args:
            id_str (str): A user defined unique id string to identify the certificate
            output_prefix (str): Output prefix of the resulting chain certificate in format
                                 <output_prefix>.cert.pem
            prefixes (str): Prefixes of certificate files to chain together
            certs_dir (str): Valid directory path where the certificate is to be exported.

        Raises:
            edgectl.errors.EdgeValueError - Any input found to be invalid
            edgectl.errors.EdgeFileAccessError
        """
        file_names = []
        for prefix in prefixes:
            if prefix not in list(self._cert_chain.keys()):
                msg = 'Invalid cert ID: {0}'.format(prefix)
                raise edgectl.errors.EdgeValueError(msg)
            else:
                cert_dict = self._cert_chain[prefix]
                if 'ca_chain' in list(cert_dict.keys()):
                    # this cert contains an existing certificate chain
                    # pick the chain instead of the actual cert
                    cert_file_name = prefix + '-chain.cert.pem'
                else:
                    cert_file_name = prefix + '.cert.pem'
                cert_file = os.path.join(certs_dir, prefix, 'cert', cert_file_name)
                path = os.path.realpath(cert_file)
                file_names.append(path)
        try:
            output_dir = os.path.join(certs_dir, output_prefix)
            logging.debug('Deleting dir: %s', output_dir)
            EdgeUtils.delete_dir(output_dir)
            logging.debug('Creating dir: %s', output_dir)
            EdgeUtils.mkdir_if_needed(output_dir)
            output_dir = os.path.join(output_dir, 'cert')
            logging.debug('Creating dir: %s', output_dir)
            EdgeUtils.mkdir_if_needed(output_dir)
            output_file_name = os.path.join(output_dir, output_prefix + '.cert.pem')
            logging.debug('Exporting Chain Certificate File: %s', output_file_name)
            with open(output_file_name, 'wb') as op_file:
                for file_name in file_names:
                    logging.debug('Chaining File: %s', file_name)
                    with open(file_name, 'rb') as ip_file:
                        op_file.write(ip_file.read())
        except IOError as ex:
            msg = 'IO Error when creating chain cert: {0}.' \
                  ' Errno: {1} Error: {2}'.format(output_file_name, str(ex.errno), ex.strerror)
            logging.error(msg)
            raise edgectl.errors.EdgeFileAccessError(msg, output_file_name)

def _get_kwargs_files(kwarg_key, file_type_msg, **kwargs):
    file_path = None
    try:
        file_path = kwargs[kwarg_key]
        if EdgeUtils.check_if_file_exists(file_path) is False:
            msg = 'File does not exist: {0}'.format(file_path)
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)
        return file_path
    except KeyError:
        msg = '{0} file is required'.format(file_type_msg)
        logging.error(msg)
        raise edgectl.errors.EdgeValueError(msg)

def _get_kwargs_validity(kwarg_key, min_validity, max_validity, **kwargs):
    validity_days_from_now = 365
    if kwarg_key in kwargs:
        validity_days_from_now = kwargs[kwarg_key]

    if validity_days_from_now < min_validity or validity_days_from_now > max_validity:
        msg = 'Certificate validity days needs to greater than or equal to {0} ' \
              'and less than {1} days. Value provided: {2}'.format(min_validity,
                                                                   max_validity,
                                                                   validity_days_from_now)
        logging.error(msg)
        raise edgectl.errors.EdgeValueError(msg)

    return validity_days_from_now

def _validate_string_length(test_string, min_length, max_length):
    length = len(test_string)
    if min_length <= length and length <= max_length:
        return True
    return False

def _get_kwargs_passphrase(kwarg_key, min_length, max_length, **kwargs):
    passphrase = None
    if kwarg_key in kwargs:
        passphrase = kwargs[kwarg_key]
    if passphrase is not None:
        if _validate_string_length(passphrase, min_length, max_length) is False:
            msg = 'Private key passphrase needs to greater than or equal to {0} and less ' \
                  'than {1} characters.'.format(min_length, max_length)
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)
    return passphrase

def _get_kwargs_string(kwarg_key, min_length, max_length, default_str=None, **kwargs):
    result_str = default_str
    if kwarg_key in kwargs:
        result_str = kwargs[kwarg_key]
    if result_str is not None:
        if _validate_string_length(result_str, min_length, max_length) is False:
            msg = 'KWarg[{0}]:{1} string length needs to greater than or equal to {2} and less ' \
                  'than {3} characters.'.format(kwarg_key, result_str, min_length, max_length)
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)
    return result_str
