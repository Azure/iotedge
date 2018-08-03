Name:           iotedge
Version:        @version@
Release:        1%{?dist}
Summary:        Blah

License:        Blah
URL:            https://github.com/azure/iotedge
Source0:        iotedge-%{version}.tar.gz

%description
This is the description

%prep
%setup -q

%build

%install
rm -rf $RPM_BUILD_ROOT
make install-centos7 DESTDIR=$RPM_BUILD_ROOT

%clean
rm -rf $RPM_BUILD_ROOT

%files
%doc

%changelog
