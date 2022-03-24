// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    public static class CertificateHelper
    {
        public const string CertificatePem = @"-----BEGIN CERTIFICATE-----
MIIEszCCApugAwIBAgIBKTANBgkqhkiG9w0BAQsFADAdMRswGQYDVQQDDBJUZXN0
IEVkZ2UgT3duZXIgQ0EwHhcNMTgwNTIzMDAzMjQzWhcNMTgwODIxMDAzMjQzWjAd
MRswGQYDVQQDDBJUZXN0IEVkZ2UgT3duZXIgQ0EwggIiMA0GCSqGSIb3DQEBAQUA
A4ICDwAwggIKAoICAQDaDQIZm/VDDbUf5/qmO6IQiKPteetPdonYo0hYOyfkK+9n
99jQWO65ue5f2lU5RubjL8ewL8AtnHqQuQEvjUQPu4aMSH+VySStK27dfn8HQ6ut
39OuZIWqs1rWP5VRfY6inXUaZxmfllj1skStkqSJNCcW0Bp4xtt85V6nKTbEKj8F
UI1aCG3lfdMRFfxontJv/vu+91lQ10pucoUfKrpw6C4BHIMzpY8hZKVnVki8Y60b
eLAGqYA85Nlb5xbCAkErDEkjRf7b/y474MJHhlhzELsfhaswqYqoF83+PMPYXBkr
8BM4QAFO9rSep8CBHvrA0zC3fbMwsDCPfk4x3GyCtVg90TZbj2bKN61elLkNKhgE
S60uuegz8zHYqpy/bAj0XwnwBYguQ2RiYVeOTw7ubE+0VzVLp5ISJJsise6AafvM
ghJVfPjONodAs5Bw9mai61RdxGdgLWnE6sXhfBKQ5rl9Z8NlbrGCTQVgdtJK4oti
w66T71iqS69IDh85GU3gWNoYf2L4i6x/KOI3o+5anK+u06YFzpXLvtDcMnxug11Q
9C8DlcCEBAB6DG6kwFPTv2qlBLz10SMBJ3wrVu4sb9I7MYoNh1Jytg/XRM/zZkCu
o3jA+Jr6Dubw9TdYnP8YtP2A9vYSnZkiu4HaY3MhkXDC1cpWFa8rGqNxbS/lqQID
AQABMA0GCSqGSIb3DQEBCwUAA4ICAQAWpeB8H7H7das8+uhjugdvu406kCnCUC6T
NwvzHBj3naUUTJhMYj5fmT3C4DnyEdr257UfQZ3v1hBGxXdg4ZRyfHycvEKhP6LL
E/9J9LrMy6G/QFAH8y4IAtNtLeDURBcCBg+ZqaDdIm1e4QY1qHcfH/pBG6tj0Dnk
Y4GRSM/ClAzlzhoLqFE/wYNM4VKHJpWt2fw2y3hP4saNEJlKqDkD4ezHJ5zW75W+
XYJoWo+giOkkajH+GzVdpScT8OpiOM5wJQ0qn/OLTu0oZZMDClhNeCNRYYWvT7pB
am/REGW2DZ6twZbfQXkUjf52qQ7LK31OqY59zLajE+O2g9aJ9PGiFKh925Gn6Vw2
b+VWn/Eb/qDq/ItrW+FgGZ+dyAxRaZGIugDNOw46juM90yVLJizlkScHf9pLU7uO
M1E9d71JkHrKc8XOkPtJ6kx3VYScJ0rhCJNQhbAxPYd+ahux3wiCbZcO2CyeyZQx
XL0MLwajpaUPCbZ2tALRjoeH73B95CMGYYXSGp4d9a/ttprXqFEuHEGbJKvHPTZ2
x2RXGozmy6wV2uM0vc3fyUu+rse2HmBbDfVXul3EsXl3JmImdySc65CR/gczbULm
P32rJqhICRJq9XTuRbrpeVFSazHwYsfwfaoh3oKh8CXZnyvibZ1lpER+DSn+P8mi
jakkDyV11Q==
-----END CERTIFICATE-----
";

        public const string CertificateThumbprintSha1 = "4b763ae53da1f86112acb008cac657b8dd259e76";
        public const string CertificateThumbprintSha256 = "1826331953f481879eec6730c565e8849360a9d3b9d230071da29e3fe9751071";

        // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic key used in tests")]
        public const string PrivateKeyPemPkcs8 = @"-----BEGIN PRIVATE KEY-----
MIIJQgIBADANBgkqhkiG9w0BAQEFAASCCSwwggkoAgEAAoICAQDaDQIZm/VDDbUf
5/qmO6IQiKPteetPdonYo0hYOyfkK+9n99jQWO65ue5f2lU5RubjL8ewL8AtnHqQ
uQEvjUQPu4aMSH+VySStK27dfn8HQ6ut39OuZIWqs1rWP5VRfY6inXUaZxmfllj1
skStkqSJNCcW0Bp4xtt85V6nKTbEKj8FUI1aCG3lfdMRFfxontJv/vu+91lQ10pu
coUfKrpw6C4BHIMzpY8hZKVnVki8Y60beLAGqYA85Nlb5xbCAkErDEkjRf7b/y47
4MJHhlhzELsfhaswqYqoF83+PMPYXBkr8BM4QAFO9rSep8CBHvrA0zC3fbMwsDCP
fk4x3GyCtVg90TZbj2bKN61elLkNKhgES60uuegz8zHYqpy/bAj0XwnwBYguQ2Ri
YVeOTw7ubE+0VzVLp5ISJJsise6AafvMghJVfPjONodAs5Bw9mai61RdxGdgLWnE
6sXhfBKQ5rl9Z8NlbrGCTQVgdtJK4otiw66T71iqS69IDh85GU3gWNoYf2L4i6x/
KOI3o+5anK+u06YFzpXLvtDcMnxug11Q9C8DlcCEBAB6DG6kwFPTv2qlBLz10SMB
J3wrVu4sb9I7MYoNh1Jytg/XRM/zZkCuo3jA+Jr6Dubw9TdYnP8YtP2A9vYSnZki
u4HaY3MhkXDC1cpWFa8rGqNxbS/lqQIDAQABAoICAAvPtJNqjUiKj4sg58Tlagv3
Otn8RrDRPPpNLfgJjEmhz6AUHtx6VMQevDjY/NDTdGJODkUO8RwHY+Q/AT9wKYWo
pMsoijC06pWuypyY44yjL8OFYlQKAeuTN5Jvc0kswfMxEEzT1OF+JWd5tpqoXN1J
w+xKbYSpUO5dBlmLs/nASBWjnWSJHFrYC/za8gdAwylp6H0ZrO7iGpgNAAUGLX88
NHG+96RujWhDqWoFlH8P7yqTyQUzXUzvII8H34W21YzdZ4DPo9SK6Bg6PovdTSE+
gMReWz2RkX81euUQqZMoufxVTtU3MlrypioJ8DWOVgrn5bWqy3ARuy+qqdWtmPsJ
+rY0Yji/YiKV6O7a6HF4fRhWFFzAiJid76HHkFC6M9VK0DnVJBJH6ItDbxw+jPIQ
Jbur2VoRAdhPke35f7V6p1j4PmjrEjiqMzDTyUjy1gTrRWZLPRn2uwvazk0okEPs
64r+KGaSSSKtTYN+JQn0jYE87qhHDyAVMLBTh4ZMrDd+itHXOQ85lsotx7wMOMCX
BJutLi0ApFS4YbsCPy2XwiR8HDveA+VqtT4s1lbdGiX6otNvbRljKfFY7bcWmm90
0PA8sjHFUAXCv6nOuxjwb/uZ+1LhhZxLcagw7aIapHHw/+Aq6QsEJBBunadDYgfr
LWQre6fIS1Erz0rINkt5AoIBAQDuq7n0zfsqVdgpX+HcP+vr5R+hDv4bRi8ImKCY
ioLHc8PozKZz+6mk5nzfIHYNMknFQMuy3/D8dw5s0kzHLHAjtOdhYQQcIpA3V3ZU
PVUhZ7dGxFfkrjRlRBXJgKDl71G5/oJQbQYPJkaBT4kGz8bSyBakx9kZR/E9bzB2
/ydYpmRt78kPhqyDQ+LlkRUWhR7XpwLrB53a80Vf+ATeExAGrsjl8E9Fa9kIWbSo
5LW0yeHGkk8/ToGRKIsHmTAznLAwyp6tuYGVaBBnZJ5s1K0ScCnm1YMXwiuhzYfw
9eK82hQW8ZwEgkTle/CX3W4MGzTzk8ciC0RdCPwQpehnTiS7AoIBAQDp4gJpbet0
3glF3TmF5pHQ1c7yriOTJFDigQJ6DQYWn+X+thoFFJBycY2IY9PrPamfI/qOVl6u
SO1nuNwEBTO6UZhI+ex1PBWVVe+oAZGC3JzNf+as2vouL5frm4BqepyxBfv6ZE9Q
kMn4aAv10Aef4ghNo0oSie8eDzwQSQqr2TrROHPsQxjuKJdRdQ86tlXkGaPY6Ks+
9/PJQLoypj5mx1IY3aRuWL9GdPMaPDfGYcXIU8lr+SPlvdmTxGdtoPL/ZdvbLx/0
q7Z6svhYtaM9+FJnn0kRIH/kO54xjh0oN7mpBVHsvYIPSt+8za4tbL47sg0XIVbz
/Z90G1jjMKrrAoIBAQCloLajlG5AquIflFKBLjrisVaJxoXBF6t8I68PLNAk6cmC
vMKmqnbH4Mu3bCeAcO2Q3a5+q7no+hYgnrB5Z/VKUjhf85uOis3aGfAb9ZQmYntl
uMvl/p6Nx/n2pDUEXFgy4tQ8S+xwhvdWtYM6HuazT/em0qluSea343mWmusLMi1v
vX+iLqt5TJshBNXFkwwcS+JSiC6by0bRmqSGGGR+vrzcFTBt1LIAgYBF1LHkjFUK
IG6uWCTCP4h79Wrl5k6/DV2g4aNzs4vutHzcuZqBuSTa9EDNNApjduZn6bs3o39d
jL3gwyZcuu3z9c5wyFCu2FbQ4VDH33xNcVUem7QRAoIBACMfj+EpYrzQQ3A8gtD7
CVblZQjI4grM32DEowyVPB7VsIKJ8mpk5jRpnSmoZEDlp72Ad7Y8fkeKKCz1dAUe
iuAmNMpwzfPlLBCbMTx3z9RpMRsjZA79a6jX+OanGafj9fgXv/mgatDcjZhCd9lY
fmyiU0DljtAt6r0G6KxBa9rW6qBU7APFJ89MRT00aS8WBtwUhaijeGQidHf6wnus
v55LvKaDUphHt6HrGj8MYAvozv0AqDUQ2zU7R5uLWUT7cMKuF1BZSWFDEEpo6ibY
UEWULzvkjeKGkO5DjcQ/ZV2O0NDzPZRh+VA2nFcMRGYJ+J+aY6DfnuFRa0rSeIzV
2DUCggEAVLWjRdzp2E7UaexFBbcTAsdfS6oVsSsyZqfo2dLaza5Y8C65Wq+doVu1
0ZWrZD68oF/glNBWMKf1fWC2+8wHboGsnKbi85K2xIwAc9jcJNuT8Z/8s4lrNujL
wMuvxR3EYp0C4Uh1oeQKWohyjoM4B9JCuak1dSUgZmRCKYGLfVCjAL1GrOsVeWBN
dAW3OsRJkgl7OWgnzxnyaysZzP3ULjM7rhHQIBhpgGh1kVIFUyv2GdHqS/BpF8cK
dfjGFy1v/NqzATNcHpZVmqDT9CsZutBEwjdJyA+BcTfqkmb+alItJU8OsZu6c9nO
U7JoTvzy0x7VG98T0+y68IcyjsSIPQ==
-----END PRIVATE KEY-----
";

        // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic key used in tests")]
        public const string PrivateKeyPemPkcs1 = @"-----BEGIN RSA PRIVATE KEY-----
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
-----END RSA PRIVATE KEY-----";

        public const string ECCCertificatePem = @"-----BEGIN CERTIFICATE-----
MIIBejCCASACCQD758oFRTpGozAKBggqhkjOPQQDAjBFMQswCQYDVQQGEwJBVTET
MBEGA1UECAwKU29tZS1TdGF0ZTEhMB8GA1UECgwYSW50ZXJuZXQgV2lkZ2l0cyBQ
dHkgTHRkMB4XDTIwMDgxODIyMDIzMFoXDTIxMDgxODIyMDIzMFowRTELMAkGA1UE
BhMCQVUxEzARBgNVBAgMClNvbWUtU3RhdGUxITAfBgNVBAoMGEludGVybmV0IFdp
ZGdpdHMgUHR5IEx0ZDBZMBMGByqGSM49AgEGCCqGSM49AwEHA0IABL8DMFmDTOmF
pcCzDqRMWBaG8VVEFXZoBFwO752RBXb8yr6zXoHPh0ET08t0PZasT4jiWci5Pc6F
+u3Gdb65PYAwCgYIKoZIzj0EAwIDSAAwRQIgIMsSviG0HTcQtz0EC5/2+mKoUn+d
x0DnmhDakf0O/kICIQC0DzYCSXsk0Yce1+Bi7zmwjp320U7o0sCs7O8ZhUgy+g==
-----END CERTIFICATE-----
";

        // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic key used in tests")]
        public const string ECCPrivateKeyPemPkcs1 = @"-----BEGIN EC PRIVATE KEY-----
MHcCAQEEIP+wX2mlEdZCqURmTFq05cV0XE6VefkqCshhc88q8mxMoAoGCCqGSM49
AwEHoUQDQgAEvwMwWYNM6YWlwLMOpExYFobxVUQVdmgEXA7vnZEFdvzKvrNegc+H
QRPTy3Q9lqxPiOJZyLk9zoX67cZ1vrk9gB==
-----END EC PRIVATE KEY-----
";

        // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic key used in tests")]
        public const string ECCPrivateKeyPemPkcs8 = @"-----BEGIN PRIVATE KEY-----
MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQg/7BfaaUR1kKpRGZM
WrTlxXRcTpV5+SoKyGFzzyrybEyhRANCAAS/AzBZg0zphaXAsw6kTFgWhvFVRBV2
aARcDu+dkQV2/Mq+s16Bz4dBE9PLdD2WrE+I4lnIuT3OhfrtxnW+uT2A
-----END PRIVATE KEY-----";

        public enum ExtKeyUsage
        {
            None = 0,
            ClientAuth,
            ServerAuth,
        }

        public static X509Certificate2 GetCertificate(
            string thumbprint,
            StoreName storeName,
            StoreLocation storeLocation)
        {
            Preconditions.CheckNonWhiteSpace(thumbprint, nameof(thumbprint));
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection col = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                thumbprint,
                false);
            if (col != null && col.Count > 0)
            {
                return col[0];
            }
            return null;
        }

        public static X509Certificate2 GenerateSelfSignedCert(string subjectName, bool isCA = false)
        {
            return GenerateSelfSignedCert(subjectName, DateTime.Now.Subtract(TimeSpan.FromDays(2)), DateTime.Now.AddYears(10), isCA);
        }

        public static X509Certificate2 GenerateSelfSignedCert(string subjectName, DateTime notBefore, DateTime notAfter, bool isCA) =>
            GenerateCertificate(subjectName, notBefore, notAfter, null, isCA, null, null);

        public static X509Certificate2 GenerateServerCert(string subjectName, DateTime notBefore, DateTime notAfter) =>
            GenerateCertificate(subjectName, notBefore, notAfter, null, false, null, new List<ExtKeyUsage>() { ExtKeyUsage.ServerAuth });

        public static X509Certificate2 GenerateClientert(string subjectName, DateTime notBefore, DateTime notAfter) =>
            GenerateCertificate(subjectName, notBefore, notAfter, null, false, null, new List<ExtKeyUsage>() { ExtKeyUsage.ClientAuth });

        public static X509Certificate2 GenerateCertificate(
                                            string subjectName,
                                            DateTime notBefore,
                                            DateTime notAfter,
                                            X509Certificate2 issuer,
                                            bool isCA,
                                            SubjectAlternativeNameBuilder sanEntries,
                                            IList<ExtKeyUsage> extKeyUsages)
        {
            X500DistinguishedName distinguishedName = new X500DistinguishedName($"CN={subjectName}");

            using (RSA rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                var keyUsageExtensions = X509KeyUsageFlags.DigitalSignature
                                       | X509KeyUsageFlags.KeyEncipherment
                                       | X509KeyUsageFlags.DataEncipherment;

                if (isCA)
                {
                    keyUsageExtensions |= X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign;
                    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
                }

                request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsageExtensions, false));

                if (extKeyUsages != null)
                {
                    if (extKeyUsages.Contains(ExtKeyUsage.ClientAuth))
                    {
                        request.CertificateExtensions.Add(
                                    new X509EnhancedKeyUsageExtension(
                                            new OidCollection { new Oid("1.3.6.1.5.5.7.3.8") }, false));
                    }

                    if (extKeyUsages.Contains(ExtKeyUsage.ServerAuth))
                    {
                        request.CertificateExtensions.Add(
                                    new X509EnhancedKeyUsageExtension(
                                            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
                    }
                }

                if (sanEntries != null)
                {
                    request.CertificateExtensions.Add(sanEntries.Build());
                }

                if (issuer == null)
                {
                    return request.CreateSelfSigned(notBefore, notAfter);
                }
                else
                {
                    var serialNumber = new byte[6];
                    new Random().NextBytes(serialNumber);
                    return request.Create(issuer, notBefore, notAfter, serialNumber);
                }
            }
        }
    }
}
