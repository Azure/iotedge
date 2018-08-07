%define iotedge_user iotedge
%define iotedge_group %{iotedge_user}
%define iotedge_home %{_localstatedir}/lib/iotedge
%define iotedge_logdir %{_localstatedir}/log/iotedge
%define iotedge_rundir %{_localstatedir}/run/iotedge
%define iotedge_confdir %{_sysconfdir}/iotedge
%define iotedge_datadir %{_datadir}/iotedge

Name:           iotedge
Version:        @version@
Release:        @revision@%{?dist}

License:        Proprietary
Summary:        Azure IoT Edge Security Daemon
URL:            https://github.com/azure/iotedge

BuildRequires:  systemd
Requires(pre):  shadow-utils
Source0:        iotedge-%{version}.tar.gz

%description
Azure IoT Edge Security Daemon
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
make install DESTDIR=$RPM_BUILD_ROOT unitdir=%{_unitdir}

%clean
rm -rf $RPM_BUILD_ROOT

%pre
/usr/bin/getent group %{iotedge_group} || %{_sbindir}/groupadd -r %{iotedge_group}
/usr/bin/getent passwd %{iotedge_user} || \
    %{_sbindir}/useradd -r -g %{iotedge_group} -c "iotedge user" \
    -s /bin/nologin -d %{iotedge_home} %{iotedge_user}
exit 0

%files
%defattr(-, root, root, -)

# bins
%{_bindir}/iotedge
%{_bindir}/iotedged

# config
%attr(400, %{iotedge_user}, %{iotedge_group}) %config(noreplace) %{iotedge_confdir}/config.yaml
%config(noreplace) %{_sysconfdir}/logrotate.d/%{name}

# man
%{_mandir}/man1/iotedge.1.gz
%{_mandir}/man8/iotedged.8.gz

# systemd
%{_unitdir}/%{name}.service

# sockets
%attr(660, %{iotedge_user}, %{iotedge_group}) %{iotedge_rundir}/mgmt.sock
%attr(666, %{iotedge_user}, %{iotedge_group}) %{iotedge_rundir}/workload.sock

# dirs
%attr(-, %{iotedge_user}, %{iotedge_group}) %dir %{iotedge_home}
%attr(-, %{iotedge_user}, %{iotedge_group}) %dir %{iotedge_logdir}
%attr(-, %{iotedge_user}, %{iotedge_group}) %dir %{iotedge_rundir}

%doc %{_docdir}/%{name}/LICENSE.gz
%doc %{_docdir}/%{name}/ThirdPartyNotices.gz
%doc %{_docdir}/%{name}/trademark

%changelog
