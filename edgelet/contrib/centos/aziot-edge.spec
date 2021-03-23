%define iotedge_user iotedge
%define iotedge_group %{iotedge_user}
%define iotedge_home %{_localstatedir}/lib/aziot/edged
%define iotedge_logdir %{_localstatedir}/log/aziot/edged
%define iotedge_socketdir %{_localstatedir}/lib/iotedge
%define aziot_confdir %{_sysconfdir}/aziot
%define iotedge_confdir %{aziot_confdir}/edged

Name:           aziot-edge
Version:        @version@
Release:        @release@%{?dist}

License:        MIT
Summary:        Azure IoT Edge Module Runtime
URL:            https://github.com/azure/iotedge

%{?systemd_requires}
BuildRequires:  systemd
Requires(pre):  shadow-utils
Requires:       aziot-identity-service >= 1.2.0~rc4-1
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
make \
    CONNECT_MANAGEMENT_URI=unix://%{iotedge_socketdir}/mgmt.sock \
    CONNECT_WORKLOAD_URI=unix://%{iotedge_socketdir}/workload.sock \
    LISTEN_MANAGEMENT_URI=unix://%{iotedge_socketdir}/mgmt.sock \
    LISTEN_WORKLOAD_URI=unix://%{iotedge_socketdir}/workload.sock \
    release

%install
rm -rf $RPM_BUILD_ROOT
make \
    CONNECT_MANAGEMENT_URI=unix://%{iotedge_socketdir}/mgmt.sock \
    CONNECT_WORKLOAD_URI=unix://%{iotedge_socketdir}/workload.sock \
    LISTEN_MANAGEMENT_URI=unix://%{iotedge_socketdir}/mgmt.sock \
    LISTEN_WORKLOAD_URI=unix://%{iotedge_socketdir}/workload.sock \
    DESTDIR=$RPM_BUILD_ROOT \
    unitdir=%{_unitdir} \
    docdir=%{_docdir}/%{name} \
    install

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
if [ ! -f '/etc/aziot/config.toml' ]; then
    echo "==============================================================================="
    echo ""
    echo "                              Azure IoT Edge"
    echo ""
    echo "  IMPORTANT: Please configure the device with provisioning information."
    echo ""

    if [ -f '/etc/iotedge/config.yaml' ]; then
        echo "  Detected /etc/iotedge/config.yaml from a previously installed version of IoT Edge."
        echo "  You can import the previous configuration using:"
        echo ""
        echo "    iotedge config import"
        echo ""
        echo "Alternatively, copy the configuration file at /etc/aziot/config.toml.edge.template to /etc/aziot/config.toml,"
    else
        echo "Copy the configuration file at /etc/aziot/config.toml.edge.template to /etc/aziot/config.toml,"
    fi

    echo "  update it with your device information, then apply your configuration changes with:"
    echo ""
    echo "    iotedge config apply"
    echo ""
    echo "  You may need to run iotedge config commands with sudo, depending on your environment."
    echo ""
    echo "==============================================================================="
fi
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
%attr(600, root, root) %{aziot_confdir}/config.toml.edge.template
%attr(400, %{iotedge_user}, %{iotedge_group}) %{iotedge_confdir}/config.toml.default
%attr(700, %{iotedge_user}, %{iotedge_group}) %dir %{iotedge_confdir}/config.d
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
