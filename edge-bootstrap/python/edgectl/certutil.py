import logging
import errno
import os
from OpenSSL import crypto

class EdgeCertUtil(object):
    def __init__(self):
        return

    def create_root_ca_cert(self,
                            id_str,
                            validity_days_from_now,
                            subj_dict):
        pass

    def create_intermediate_ca_cert(self,
                                    id_str,
                                    validity_days_from_now,
                                    issuer_id_str,
                                    common_name,
                                    path_len_zero):
        pass

    def create_server_cert(self,
                           id_str,
                           validity_days_from_now,
                           issuer_id_str,
                           hostname):
        pass

    def export_cert_artifacts_to_dir(self, id_str, dir_path):
        pass

class EdgeCertUtilPyOpenSSL(EdgeCertUtil):
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
                            subj_dict):
        if id_str in self._cert_chain:
            raise ValueError('Duplicate CA Cert Gen Request. ID:' + id_str)
        key_obj = self.__create_key_pair(self.TYPE_RSA, self.CA_KEY_LEN)
        csr_obj = self.__create_csr(key_obj,
                                    C=subj_dict['country'],
                                    ST=subj_dict['state'],
                                    L=subj_dict['locality'],
                                    O=subj_dict['org'],
                                    OU=subj_dict['org_unit'],
                                    CN=subj_dict['common_name'])

        utc_secs = validity_days_from_now * 24 * 60 * 60
        cert_obj = self.__create_ca_cert(csr_obj,
                                         csr_obj,
                                         key_obj,
                                         self._serial_number,
                                         (0, utc_secs),
                                         False)
        self._serial_number += 1
        cert_dict = {}
        cert_dict['key_pair'] = key_obj
        cert_dict['csr'] = csr_obj
        cert_dict['cert'] = cert_obj
        cert_dict['issuer_id'] = id_str
        self._cert_chain[id_str] = cert_dict
        return

    def create_intermediate_ca_cert(self,
                                    id_str,
                                    validity_days_from_now,
                                    issuer_id_str,
                                    common_name,
                                    path_len_zero):
        if id_str in self._cert_chain:
            raise ValueError('Duplicate Intermediate Cert Gen Request. ID:' + id_str)
        if issuer_id_str not in self._cert_chain:
            raise KeyError('Invalid Issuer Cert ID:' + issuer_id_str)

        issuer_cert_dict = self._cert_chain[issuer_id_str]
        issuer_cert = issuer_cert_dict['cert']
        issuer_key = issuer_cert_dict['key_pair']
        key_obj = self.__create_key_pair(self.TYPE_RSA, self.CA_KEY_LEN)
        csr_obj = self.__create_csr(key_obj,
                                    C=issuer_cert.get_subject().countryName,
                                    ST=issuer_cert.get_subject().stateOrProvinceName,
                                    L=issuer_cert.get_subject().localityName,
                                    O=issuer_cert.get_subject().organizationName,
                                    OU=issuer_cert.get_subject().organizationalUnitName,
                                    CN=common_name)
        utc_secs = validity_days_from_now * 24 * 60 * 60
        cert_obj = self.__create_ca_cert(csr_obj,
                                         issuer_cert,
                                         issuer_key,
                                         self._serial_number,
                                         (0, utc_secs),
                                         path_len_zero)
        self._serial_number += 1
        cert_dict = {}
        cert_dict['key_pair'] = key_obj
        cert_dict['csr'] = csr_obj
        cert_dict['cert'] = cert_obj
        cert_dict['issuer_id'] = issuer_id_str
        self._cert_chain[id_str] = cert_dict
        return

    def create_server_cert(self,
                           id_str,
                           validity_days_from_now,
                           issuer_id_str,
                           hostname):
        if id_str in self._cert_chain:
            raise ValueError('Duplicate Server Cert Gen Request. ID:' + id_str)
        if issuer_id_str not in self._cert_chain:
            raise KeyError('Invalid Issuer Cert ID:' + issuer_id_str)

        issuer_cert_dict = self._cert_chain[issuer_id_str]
        issuer_cert = issuer_cert_dict['cert']
        issuer_key = issuer_cert_dict['key_pair']
        key_obj = self.__create_key_pair(self.TYPE_RSA, self.SERVER_KEY_LEN)
        csr_obj = self.__create_csr(key_obj,
                                    C=issuer_cert.get_subject().countryName,
                                    ST=issuer_cert.get_subject().stateOrProvinceName,
                                    L=issuer_cert.get_subject().localityName,
                                    O=issuer_cert.get_subject().organizationName,
                                    OU=issuer_cert.get_subject().organizationalUnitName,
                                    CN=hostname)
        utc_secs = validity_days_from_now * 24 * 60 * 60
        cert_obj = self.__create_server_cert(csr_obj,
                                             issuer_cert,
                                             issuer_key,
                                             self._serial_number,
                                             (0, utc_secs))
        self._serial_number += 1
        cert_dict = {}
        cert_dict['key_pair'] = key_obj
        cert_dict['csr'] = csr_obj
        cert_dict['cert'] = cert_obj
        cert_dict['issuer_id'] = issuer_id_str
        self._cert_chain[id_str] = cert_dict
        return

    def export_cert_artifacts_to_dir(self, id_str, dir_path):
        if os.path.exists(dir_path) is False:
            raise ValueError('Invalid Directory Name:' + dir_path)
        elif id_str not in self._cert_chain:
            raise KeyError('Invalid Cert ID:' + id_str)
        else:
            cert_dict = self._cert_chain[id_str]
            prefix = id_str
            try:
                path = os.path.realpath(dir_path)
                path = os.path.join(path, prefix)
                self.__mkdir_if_needed(path)
                priv_dir = os.path.join(path, 'private')
                self.__mkdir_if_needed(priv_dir)
                os.chmod(priv_dir, 0o700)
                cert_dir = os.path.join(path, 'cert')
                self.__mkdir_if_needed(cert_dir)
                csr_dir = os.path.join(path, 'csr')
                self.__mkdir_if_needed(csr_dir)

                # export the private key
                key_obj = cert_dict['key_pair']
                priv_key_file_name = prefix + '.key.pem'

                priv_key_file = os.path.join(priv_dir, priv_key_file_name)
                with open(priv_key_file, 'w') as ip_file:
                    ip_file.write(crypto.dump_privatekey(crypto.FILETYPE_PEM,
                                                         key_obj).decode('utf-8'))
                # export the cert
                cert_obj = cert_dict['cert']
                cert_file_name = prefix + '.cert.pem'
                cert_file = os.path.join(cert_dir, cert_file_name)

                with open(cert_file, 'w') as ip_file:
                    ip_file.write(crypto.dump_certificate(crypto.FILETYPE_PEM,
                                                          cert_obj).decode('utf-8'))

                # export the csr
                csr_obj = cert_dict['csr']
                csr_file_name = prefix + '.csr.pem'
                csr_file = os.path.join(csr_dir, csr_file_name)

                with open(csr_file, 'w') as ip_file:
                    ip_file.write(crypto.dump_certificate_request(crypto.FILETYPE_PEM,
                                                                  csr_obj).decode('utf-8'))
            except OSError as ex:
                logging.error('Error Observed When Export Cert Id: '
                              + id_str + '. Errno '
                              + str(ex.errno) + ', Error:' + ex.strerror)
                raise

    def export_pfx_cert(self, id_str, dir_path):
        if id_str not in self._cert_chain:
            raise KeyError('Invalid Cert ID:' + id_str)
        else:
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
            pfx_output_file_name = os.path.join(cert_dir, prefix + '.pfx')
            with open(pfx_output_file_name, 'w') as pfx_file:
                pfx_file.write(str(pfx_data))

    @staticmethod
    def __mkdir_if_needed(dir_path):
        try:
            os.mkdir(dir_path)
        except OSError as e:
            if e.errno != errno.EEXIST:
                raise

    @staticmethod
    def __create_key_pair(type, key_bit_len):
        key_pair = crypto.PKey()
        key_pair.generate_key(EdgeCertUtilPyOpenSSL._type_dict[type], key_bit_len)
        return key_pair

    @staticmethod
    def __create_csr(key_pair, **kwargs):
        csr = crypto.X509Req()
        subj = csr.get_subject()
        for key, value in list(kwargs.items()):
            setattr(subj, key, value)
        csr.set_pubkey(key_pair)
        csr.sign(key_pair, EdgeCertUtilPyOpenSSL.DIGEST)
        return csr

    @staticmethod
    def __create_cert_common(csr,
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
    def __create_ca_cert(csr,
                         issuer_cert,
                         issuer_key_pair,
                         serial_num,
                         validity_period,
                         path_len_zero):
        cert = EdgeCertUtilPyOpenSSL.__create_cert_common(csr,
                                                          issuer_cert,
                                                          serial_num,
                                                          validity_period)
        val = b'CA:TRUE'
        if path_len_zero:
            val += b', pathlen:0'
        extensions = []
        extensions.append(crypto.X509Extension(b'basicConstraints',
                                               critical=True, value=val))
        extensions.append(crypto.X509Extension(b'subjectKeyIdentifier',
                                               critical=False, value=b'hash',
                                               subject=cert))
        val = b'digitalSignature, keyCertSign, cRLSign'
        extensions.append(crypto.X509Extension(b'keyUsage',
                                               critical=True, value=val))
        cert.add_extensions(extensions)
        # this works only if added later
        cert.add_extensions([crypto.X509Extension(b'authorityKeyIdentifier',
                                                  critical=False,
                                                  value=b'keyid:always,issuer',
                                                  issuer=cert)])
        cert.sign(issuer_key_pair, EdgeCertUtilPyOpenSSL.DIGEST)
        return cert

    @staticmethod
    def __create_server_cert(csr,
                             issuer_cert,
                             issuer_key_pair,
                             serial_num,
                             validity_period):
        cert = EdgeCertUtilPyOpenSSL.__create_cert_common(csr,
                                                          issuer_cert,
                                                          serial_num,
                                                          validity_period)

        extensions = []
        extensions.append(crypto.X509Extension(b'basicConstraints',
                                               critical=False,
                                               value=b'CA:FALSE'))
        extensions.append(crypto.X509Extension(b'subjectKeyIdentifier',
                                               critical=False,
                                               value=b'hash',
                                               subject=cert))
        val = b'digitalSignature, keyEncipherment'
        extensions.append(crypto.X509Extension(b'keyUsage',
                                               critical=True, value=val))
        extensions.append(crypto.X509Extension(b'extendedKeyUsage',
                                               critical=False,
                                               value=b'serverAuth'))
        cert.add_extensions(extensions)
        cert.add_extensions([crypto.X509Extension(b'authorityKeyIdentifier',
                                                  critical=False,
                                                  value=b'keyid,issuer:always',
                                                  issuer=cert)])
        cert.sign(issuer_key_pair, EdgeCertUtilPyOpenSSL.DIGEST)

        return cert

def get_ca_cert_file_path(certs_dir):
    result = None
    prefix = 'edge-device-ca'
    path = os.path.join(certs_dir, prefix, 'cert', prefix + '.cert.pem')
    if os.path.exists(path):
        result = path
    return result

def get_server_cert_file_path(certs_dir):
    result = None
    prefix = 'edge-hub-server'
    path = os.path.join(certs_dir, prefix, 'cert', prefix + '.pfx')
    if os.path.exists(path):
        result = path
    return result

def check_if_cert_file_exists(dir_path, prefix, sub_dir):
    path = os.path.join(dir_path, prefix, sub_dir, prefix + '.cert.pem')
    result = os.path.exists(path)
    if result:
        logging.debug('Cert File Ok:' + path)
    else:
        logging.info('Cert File Does Not Exist:' + path)

    return result

def generate_self_signed_certs(certs_dir):
    logging.info('Generating Self Signed Certificates At:' + certs_dir)
    subj_dict = {'country': 'US',
                 'state': 'Washington',
                 'locality': 'Redmond',
                 'org': 'Default Edge Organization',
                 'org_unit': 'Edge Unit',
                 'common_name': 'Edge Device CA Certificate'}

    cert_util = EdgeCertUtilPyOpenSSL()
    cert_util.create_root_ca_cert('edge-device-ca',
                                  7300,
                                  subj_dict)
    cert_util.export_cert_artifacts_to_dir('edge-device-ca',
                                           certs_dir)

    cert_util.create_intermediate_ca_cert('edge-agent-ca',
                                          3650,
                                          'edge-device-ca',
                                          'Edge Agent CA Certificate',
                                          True)
    cert_util.export_cert_artifacts_to_dir('edge-agent-ca',
                                           certs_dir)

    cert_util.create_server_cert('edge-hub-server',
                                 365,
                                 'edge-agent-ca',
                                 'marohera-x1')

    cert_util.export_cert_artifacts_to_dir('edge-hub-server',
                                           certs_dir)
    cert_util.export_pfx_cert('edge-hub-server', certs_dir)

def generate_self_signed_certs_if_needed(certs_dir):
    if os.path.exists(certs_dir) is False:
        raise ValueError('Invalid Directory Name:' + certs_dir)

    path = os.path.realpath(certs_dir)
    device_ca = check_if_cert_file_exists(path, 'edge-device-ca', 'cert')
    agent_ca = check_if_cert_file_exists(path, 'edge-agent-ca', 'cert')
    hub_server = check_if_cert_file_exists(path, 'edge-hub-server', 'cert')

    if device_ca is False or agent_ca is False or hub_server is False:
        generate_self_signed_certs(certs_dir)
