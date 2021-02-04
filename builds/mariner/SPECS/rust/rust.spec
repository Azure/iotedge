Summary:        Rust Programming Language
Name:           rust
Version:        1.47.0
Release:        1%{?dist}
License:        ASL 2.0 and MIT
URL:            https://www.rust-lang.org/
Group:          Applications/System
Vendor:         Microsoft Corporation
Distribution:   Mariner
ExclusiveArch:  x86_64
Source0:        https://static.rust-lang.org/dist/rust-%{version}-x86_64-unknown-linux-gnu.tar.gz

BuildRequires:  git
BuildRequires:  cmake
BuildRequires:  glibc
BuildRequires:  binutils
BuildRequires:  python2
BuildRequires:  curl-devel

%description
Rust Programming Language

%prep
%setup -q -n rust-%{version}-x86_64-unknown-linux-gnu

%build
export USER=root
export SUDO_USER=root

%install
./install.sh --destdir=%{buildroot} --prefix=/usr
chmod 755 %{buildroot}%{_libdir}/lib*.so

%files
%{_bindir}/*
%{_libdir}/lib*.so
%{_libdir}/rustlib/*
%{_mandir}/man1/*
%doc %{_docdir}/%{name}/html/*
%{_docdir}/%{name}/html/.stamp
%doc %{_docdir}/%{name}/*
%doc %{_docdir}/cargo/*
%doc %{_docdir}/clippy/*
%doc %{_docdir}/rls/*
%doc %{_docdir}/rustfmt/*
%{_datadir}/zsh/*
%{_prefix}%{_sysconfdir}/bash_completion.d/cargo

%changelog
*   Wed Jan 06 2021 Chad Zawistowski <chzawist@microsoft.com> 1.44.1-1
-   Downgrade to 1.44.1. Remove patchfile, use envvar instead.
*   Wed Aug 26 2020 Wei-Chen Chen <weicche@microsoft.com> 1.45.2-1
-   Update to 1.45.2. Install for x86_64 only from st rust
*   Thu Mar 19 2020 Henry Beberman <henry.beberman@microsoft.com> 1.39.0-1
-   Update to 1.39.0. Fix URL. Fix Source0 URL. License verified.
*   Thu Feb 27 2020 Henry Beberman <hebeberm@microsoft.com> 1.34.2-3
-   Set SUDO_USER and USER to allow rust to hydrate as root
*   Wed Sep 25 2019 Saravanan Somasundaram <sarsoma@microsoft.com> 1.34.2-2
-   Initial Mariner version
*   Wed May 15 2019 Ankit Jain <ankitja@vmware.com> 1.34.2-1
-   Initial build. First version
