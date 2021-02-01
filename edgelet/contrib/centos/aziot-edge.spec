%define iotedge_user iotedge
%define iotedge_group %{iotedge_user}
%define iotedge_home %{_localstatedir}/lib/aziot/edged
%define iotedge_logdir %{_localstatedir}/log/aziot/edged
%define iotedge_socketdir %{_localstatedir}/lib/iotedge
%define iotedge_confdir %{_sysconfdir}/aziot/edged

Name:           aziot-edge
Version:        @version@
Release:        @release@%{?dist}

License:        Proprietary
Summary:        Azure IoT Edge Module Runtime
URL:            https://github.com/azure/iotedge

%{?systemd_requires}
BuildRequires:  systemd
Requires(pre):  shadow-utils
Requires:       aziot-identity-service >= @version@-@release@
Source0:        aziot-edge-%{version}.tar.gz

%description
Azure IoT Edge Module Runtime
Azure IoT Edge is a fully managed service that delivers cloud intelligence
locally by deploying and running artificial intelligence (AI), Azure services,
and custom logic directly on cross-platform IoT devices. Run your IoT solution
securely and at scale—whether in the cloud or offline.

This package contains the IoT Edge daemon and CLI tool.

%prep
%setup -q

%build
make release

%install
rm -rf $RPM_BUILD_ROOT
make install DESTDIR=$RPM_BUILD_ROOT unitdir=%{_unitdir} docdir=%{_docdir}/%{name}

%clean
rm -rf $RPM_BUILD_ROOT

%pre
# Check for container runtime
if ! /usr/bin/getent group docker >/dev/null; then
    echo "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
    echo ""
    echo " ERROR: No container runtime detected."
    echo ""
    echo " Please install a container runtime and run this install again."
    echo ""
    echo "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"

    exit 1
fi

# Create iotedge group
if ! /usr/bin/getent group iotedge >/dev/null; then
    %{_sbindir}/groupadd -r %{iotedge_group}
fi

# Create iotedge user
if ! /usr/bin/getent passwd iotedge >/dev/null; then
    %{_sbindir}/useradd -r -g %{iotedge_group} -c "iotedge user" -s /bin/nologin -d %{iotedge_home} %{iotedge_user}
fi

# Add iotedge user to moby-engine group
if /usr/bin/getent group docker >/dev/null; then
    %{_sbindir}/usermod -a -G docker %{iotedge_user}
fi

# Add iotedge user to aziot-identity-service groups
if /usr/bin/getent group aziotcs >/dev/null; then
    %{_sbindir}/usermod -aG aziotcs %{iotedge_user}
fi
if /usr/bin/getent group aziotks >/dev/null; then
    %{_sbindir}/usermod -aG aziotks %{iotedge_user}
fi
if /usr/bin/getent group aziotid >/dev/null; then
    %{_sbindir}/usermod -aG aziotid %{iotedge_user}
fi
exit 0

%post
sed -i "s/hostname: \"<ADD HOSTNAME HERE>\"/hostname: \"$(hostname)\"/g" /etc/aziot/edged/config.yaml
echo "==============================================================================="
echo ""
echo "                              Azure IoT Edge"
echo ""
echo "  IMPORTANT: Please update the configuration file located at:"
echo ""
echo "    /etc/aziot/edged/config.yaml"
echo ""
echo "  with your container runtime configuration."
echo ""
echo "  To configure the Identity Service with provisioning information, use:"
echo ""
echo "    'aziot init'"
echo ""
echo "  To restart all services for provisioning changes to take effect, use:"
echo ""
echo "    'systemctl restart aziot-keyd aziot-certd aziot-identityd aziot-edged'"
echo ""
echo "  These commands may need to be run with sudo depending on your environment."
echo ""
echo "==============================================================================="
%systemd_post aziot-edged.service

%preun
%systemd_preun aziot-edged.service

%postun
%systemd_postun_with_restart aziot-edged.service

%files
%defattr(-, root, root, -)

# bins
%{_bindir}/iotedge
%{_libexecdir}/aziot/aziot-edged

# config
%attr(400, %{iotedge_user}, %{iotedge_group}) %config(noreplace) %{iotedge_confdir}/config.yaml
%config(noreplace) %{_sysconfdir}/logrotate.d/%{name}

# man
%{_mandir}/man1/iotedge.1.gz
%{_mandir}/man8/aziot-edged.8.gz

# systemd
%{_unitdir}/aziot-edged.service

# sockets
%attr(660, %{iotedge_user}, %{iotedge_group}) %{iotedge_socketdir}/mgmt.sock
%attr(666, %{iotedge_user}, %{iotedge_group}) %{iotedge_socketdir}/workload.sock

# dirs
%attr(-, %{iotedge_user}, %{iotedge_group}) %dir %{iotedge_home}
%attr(-, %{iotedge_user}, %{iotedge_group}) %dir %{iotedge_logdir}
%attr(-, %{iotedge_user}, %{iotedge_group}) %dir %{iotedge_socketdir}

%doc %{_docdir}/%{name}/LICENSE.gz
%doc %{_docdir}/%{name}/ThirdPartyNotices.gz
%doc %{_docdir}/%{name}/trademark

%changelog
