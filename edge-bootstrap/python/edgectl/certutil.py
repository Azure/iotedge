import logging
import errno
import os
from datetime import datetime
from shutil import copy2
from OpenSSL import crypto
import edgectl.errors
import edgectl.edgeconstants as EC
from edgectl.edgeutils import EdgeUtils

class EdgeCertUtil(object):
    TYPE_RSA = 0
    CA_KEY_LEN = 4096
    CA_INT_KEY_LEN = 4096
    SERVER_KEY_LEN = 2048
    DIGEST = 'sha256'
    _type_dict = {TYPE_RSA: crypto.TYPE_RSA}

    def __init__(self, serial_num=1000):
        self._cert_chain = {}
        self._serial_number = serial_num

    def create_root_ca_cert(self,
                            id_str,
                            validity_days_from_now,
                            subj_dict,
                            passphrase=None):
        if id_str in self._cert_chain:
            msg = 'Duplicate CA cert gen request. ID: {0}'.format(id_str)
            raise edgectl.errors.EdgeValueError(msg)
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

    def set_root_ca_cert(self,
                         id_str,
                         ca_cert_file_path,
                         ca_root_cert_file_path,
                         ca_root_chain_cert_file_path,
                         ca_private_key_file_path,
                         ca_passphrase=None):
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
                                                passphrase=ca_passphrase)
            ca_key_obj.check()
        except crypto.Error as ex_crpto:
            msg = 'Cryptographic error when reading private key file: {0}.' \
                  ' Error: {1}'.format(ca_private_key_file_path, str(ex_crpto))
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
        except crypto.Error as ex_crpto:
            msg = 'Crypto Error: {0}'.format(str(ex_crpto))
            logging.error(msg)
            raise edgectl.errors.EdgeValueError(msg)
        except IOError as ex:
            msg = 'Could not read certificate file: {0}.' \
                  ' Errno: {1} Error: {2}'.format(ca_private_key_file_path,
                                                  str(ex.errno), ex.strerror)
            logging.error(msg)
            raise edgectl.errors.EdgeFileAccessError(msg,
                                                     ca_private_key_file_path)

    @staticmethod
    def _get_maximum_validity_days(not_after_ts_asn1, validity_days_from_now):
        ''' Returns the least number of days between:
            - now() and the certificate expiration timestamp and
            - requested certificate expiration time expressed in days from now()
        '''
        result = 0
        try:
            expiration_date = datetime.strptime(not_after_ts_asn1, "%Y%m%d%H%M%SZ")
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

    def create_intermediate_ca_cert(self,
                                    id_str,
                                    validity_days_from_now,
                                    issuer_id_str,
                                    common_name,
                                    path_len_zero,
                                    passphrase=None):
        if id_str in self._cert_chain:
            msg = 'Duplicate intermediate cert gen request. ID: {0}'.format(id_str)
            raise edgectl.errors.EdgeValueError(msg)
        if issuer_id_str not in self._cert_chain:
            msg = 'Invalid issuer cert ID: {0}'.format(issuer_id_str)
            raise edgectl.errors.EdgeValueError(msg)
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
                                            path_len_zero)
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

    def create_server_cert(self,
                           id_str,
                           validity_days_from_now,
                           issuer_id_str,
                           hostname,
                           passphrase=None):
        if id_str in self._cert_chain:
            msg = 'Duplicate server cert gen request. ID: {0}'.format(id_str)
            raise edgectl.errors.EdgeValueError(msg)
        if issuer_id_str not in self._cert_chain:
            msg = 'Invalid issuer cert ID: {0}'.format(issuer_id_str)
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
        if os.path.exists(dir_path) is False:
            msg = 'Invalid export directory {0}'.format(dir_path)
            logging.error(msg)
            raise edgectl.errors.EdgeFileAccessError(msg, dir_path)
        elif id_str not in self._cert_chain:
            msg = 'Certificate not in chain. ID: {0}'.format(id_str)
            raise edgectl.errors.EdgeValueError(msg)
        else:
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
                    logging.debug('Copying Private Key File %s To %s',
                                  key_file_path, priv_key_file)
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
                      ' Errno: {2} Error: {3}'.format(id_str, ex.filename,
                                                      str(ex.errno), ex.strerror)
                logging.error(msg)
                raise edgectl.errors.EdgeFileAccessError(msg, path)

    def export_server_pfx_cert(self, id_str, dir_path):
        if id_str not in self._cert_chain:
            msg = 'Invalid cert ID: {0}'.format(id_str)
            raise edgectl.errors.EdgeValueError(msg)
        else:
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
                logging.debug('Exporting Server PFX Certificate File: %s', pfx_output_file_name)
                with open(pfx_output_file_name, 'wb') as pfx_file:
                    pfx_file.write(pfx_data)
            except IOError as ex:
                msg = 'IO Error when exporting PFX cert ID: {0}.' \
                      ' Errno: {1} Error: {2}'.format(id_str, str(ex.errno), ex.strerror)
                logging.error(msg)
                raise edgectl.errors.EdgeFileAccessError(msg, pfx_output_file_name)

    @staticmethod
    def _create_key_pair(type, key_bit_len):
        key_pair = crypto.PKey()
        key_pair.generate_key(EdgeCertUtil._type_dict[type], key_bit_len)
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
