%define iotedge_user iotedge
%define iotedge_group %{iotedge_user}
%define iotedge_home %{_localstatedir}/lib/iotedge
%define iotedge_logdir %{_localstatedir}/log/iotedge
%define iotedge_confdir %{_sysconfdir}/iotedge
%define iotedge_datadir %{_datadir}/iotedge

Name:           iotedge
Version:        @version@
Release:        1%{?dist}
Summary:        Blah

License:        Blah
URL:            https://github.com/azure/iotedge

Requires(pre):  shadow-utils
Source0:        iotedge-%{version}.tar.gz

%description
This is the description

%prep
%setup -q

%build

%install
rm -rf $RPM_BUILD_ROOT
make install-centos7 DESTDIR=$RPM_BUILD_ROOT
%{__install} -p -d -m 0755 %{buildroot}%{iotedge_home}

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
%attr(400, %{iotedge_user}, %{iotedge_group}) %config(noreplace) %{iotedge_confdir}/config.yaml
%config(noreplace) %{_sysconfdir}/logrotate.d/%{name}
%attr(-, %{iotedge_user}, %{iotedge_group}) %dir %{iotedge_home}
%attr(-, %{iotedge_user}, %{iotedge_group}) %dir %{iotedge_logdir}

%doc

%changelog
