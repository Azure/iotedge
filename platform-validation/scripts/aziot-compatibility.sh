#!/usr/bin/env sh

###############################################################################
# This script checks whether IoT Edge can run on a target OS
###############################################################################

#Variables for logging
OSTYPE=""
ARCH=""
VERBOSE=0
FAILURES=0
PASS=0
SKIP=0
WARNINGS=0

#Variables for shared libraries
SHARED_LIB_PATH="/usr /lib /lib32 /lib64"
SHARED_LIBRARIES_BASE="libssl.so.1.1 libcrypto.so.1.1 libdl.so.2 librt.so.1 libpthread.so.0 libc.so.6 libm.so.6 libgcc_s.so.1"
SHARED_LIBRARIES_x86_64="ld-linux-x86-64.so.2"
SHARED_LIBRARIES_aarch64="ld-linux-aarch64.so.1"
SHARED_LIBRARIES_armv7l="ld-linux-armhf.so.3"
SHARED_LIB_LIBSSSL_1_1="libssl.so.1.1"
SHARED_LIB_LIBSSSL_1_0="libssl.so.1.0"

#Variables for memory and storage numbers
#TODO : Update these numbers after Automated Run. The goal is that for every release, we would update these numbers
armv7l_iotedge_binaries_size=36.68
armv7l_iotedge_binaries_avg_memory=35.51
armv7l_iotedge_container_size=322.98
armv7l_iotedge_container_memory=164.53
x86_64_iotedge_binaries_size=42.39
x86_64_iotedge_binaries_avg_memory=54.24
x86_64_iotedge_container_size=254.96
x86_64_iotedge_container_memory=175
aarch64_iotedge_binaries_size=36.68
aarch64_iotedge_binaries_avg_memory=26.62
aarch64_iotedge_container_size=322.6
aarch64_iotedge_container_memory=210
iotedge_size_buffer=50
iotedge_memory_buffer=50

#Variable for Docker API version
MINIMUM_DOCKER_API_VERSION=1.34

POSSIBLE_CONFIGS="
	/proc/config.gz
	/boot/config-$(uname -r)
	/usr/src/linux-$(uname -r)/.config
	/usr/src/linux/.config
"

if ! command -v zgrep >/dev/null 2>&1; then
    zgrep() {
        zcat "$2" | grep "$1"
    }
fi

is_set() {
    zgrep "CONFIG_$1=[y|m]" "$CONFIG" >/dev/null
}

# ------------------------------------------------------------------------------
#  Text Formatting
#  Derived from : https://github.com/moby/moby/blob/master/contrib/check-config.sh
# ------------------------------------------------------------------------------
color() {
    codes=
    if [ "$1" = 'bold' ]; then
        codes='1'
        shift
    fi
    if [ "$#" -gt 0 ]; then
        code=
        case "$1" in
        # see https://en.wikipedia.org/wiki/ANSI_escape_code#Colors
        black) code=30 ;;
        red) code=31 ;;
        green) code=32 ;;
        yellow) code=33 ;;
        blue) code=34 ;;
        magenta) code=35 ;;
        cyan) code=36 ;;
        white) code=37 ;;
        esac
        if [ "$code" ]; then
            codes="${codes:+$codes;}$code"
        fi
    fi
    printf '\033[%sm' "$codes"
}

wrap_color() {
    text="$1"
    shift
    color "$@"
    printf '%s' "$text"
    color reset
    echo
}

wrap_good() {
    if [ $VERBOSE -eq 1 ]; then
        echo "$(wrap_color "    $1" white): $(wrap_color "  $2" green)"
    fi
}
wrap_bad() {
    echo "$(wrap_color "    $1" bold red)"
}

wrap_debug_message() {
    if [ $VERBOSE -eq 1 ]; then
        echo "$(wrap_color "    $1" white)"
    fi
}

wrap_warning_message() {
    echo "$(wrap_color "    $1" yellow)"
}

wrap_pass() {
    PASS=$((PASS + 1))
    check=$(env printf '\u2713')
    echo "$(wrap_color "$check $1 - OK" green)"
}

wrap_skip() {
    SKIP=$((SKIP + 1))
    echo "$(wrap_color "$1 - Skipped" white)"
}

wrap_fail() {
    FAILURES=$((FAILURES + 1))
    cross=$(env printf '\u2a09')
    echo "$(wrap_color "$cross $1 - Error" bold red)"
}

wrap_warning() {
    WARNINGS=$((WARNINGS + 1))
    warn="!!"
    echo "$(wrap_color "$warn $1 - Warning" yellow)"
}

# ------------------------------------------------------------------------------
#  Retrieve OS TYPE AND ARCHITECTURE (Required for Getting IoT)
#  Derived from https://sh.rustup.rs
# ------------------------------------------------------------------------------
need_cmd() {
    ret=$($1 --help >/dev/null 2>&1)
    if [ $? != 0 ]; then
        exit 1
    fi
}

check_cmd() {
    command -v "$1" >/dev/null 2>&1
}

# Run a command that should never fail. If the command fails execution
# will immediately terminate with an error showing the failing
# command.
ensure() {
    if ! "$@"; then
        exit 1
    fi
}

get_libc() {
    need_cmd ldd
    need_cmd awk
    # Detect both gnu and musl
    # Also detect glibc versions older than 2.18 and return musl for these
    # Required until we identify minimum supported version
    # TODO: https://github.com/vectordotdev/vector/issues/10807
    _ldd_version=""
    _libc_version=""
    _ldd_version=$(ldd --version 2>&1)
    if [ -z "${_ldd_version##*GNU*}" ] || [ -z "${_ldd_version##*GLIBC*}" ]; then
        _libc_version=$(echo "$_ldd_version" | awk '/ldd/{print $NF}')
        version_check=$(echo $_libc_version 2.18 | awk '{if ($1 < $2) print 1; else print 0}')
        if [ $version_check -eq 1 ]; then
            echo "musl"
        else
            echo "gnu"
        fi
    elif [ -z "${_ldd_version##*musl*}" ]; then
        echo "musl"
    else
        wrap_err "Unknown implementation of libc. ldd --version returns: ${_ldd_version}"
    fi
}

get_bitness() {
    need_cmd head
    # Architecture detection without dependencies beyond coreutils.
    # ELF files start out "\x7fELF", and the following byte is
    #   0x01 for 32-bit and
    #   0x02 for 64-bit.
    # The printf builtin on some shells like dash only supports octal
    # escape sequences, so we use those.
    _current_exe_head=""
    _current_exe_head=$(head -c 5 /proc/self/exe)
    if [ "$_current_exe_head" = "$(printf '\177ELF\001')" ]; then
        echo 32
    elif [ "$_current_exe_head" = "$(printf '\177ELF\002')" ]; then
        echo 64
    else
        wrap_err "Unknown platform bitness"
    fi
}

get_endianness() {
    cputype=$1
    suffix_eb=$2
    suffix_el=$3

    # detect endianness without od/hexdump, like get_bitness() does.
    need_cmd head
    need_cmd tail

    _current_exe_endianness
    _current_exe_endianness="$(head -c 6 /proc/self/exe | tail -c 1)"
    if [ "$_current_exe_endianness" = "$(printf '\001')" ]; then
        echo "${cputype}${suffix_el}"
    elif [ "$_current_exe_endianness" = "$(printf '\002')" ]; then
        echo "${cputype}${suffix_eb}"
    else
        wrap_err "Unknown platform endianness"
    fi
}

# ------------------------------------------------------------------------------
#
#  Compatibility Tool Checks
#
# ------------------------------------------------------------------------------
get_architecture() {
    wrap_debug_message "Checking architecture..."
    _ostype=""
    _cputype=""
    _bitness=""
    _arch=""
    _ostype="$(uname -s)"
    _cputype="$(uname -m)"

    if [ "$_ostype" = Linux ]; then
        if [ "$(uname -o)" = Android ]; then
            _ostype=Android
        fi
    fi

    if [ "$_ostype" = Darwin ] && [ "$_cputype" = i386 ]; then
        # Darwin `uname -m` lies
        if sysctl hw.optional.x86_64 | grep -q ': 1'; then
            _cputype=x86_64
        fi
    fi

    case "$_ostype" in

    Android)
        _ostype=linux-android
        ;;

    Linux)
        case $(get_libc) in
        "musl")
            _ostype=unknown-linux-musl
            ;;
        "gnu")
            _ostype=unknown-linux-gnu
            ;;
        # Fallback
        *)
            _ostype=unknown-linux-gnu
            ;;
        esac
        _bitness=$(get_bitness)
        ;;

    FreeBSD)
        _ostype=unknown-freebsd
        ;;

    NetBSD)
        _ostype=unknown-netbsd
        ;;

    DragonFly)
        _ostype=unknown-dragonfly
        ;;

    Darwin)
        _ostype=apple-darwin
        ;;

    MINGW* | MSYS* | CYGWIN*)
        _ostype=pc-windows-gnu
        ;;

    *)
        err "unrecognized OS type: $_ostype"
        ;;

    esac

    case "$_cputype" in

    i386 | i486 | i686 | i786 | x86)
        _cputype=i686
        ;;

    xscale | arm)
        _cputype=arm
        if [ "$_ostype" = "linux-android" ]; then
            _ostype=linux-androideabi
        fi
        ;;

    armv6l)
        _cputype=arm
        if [ "$_ostype" = "linux-android" ]; then
            _ostype=linux-androideabi
        else
            _ostype="${_ostype}eabihf"
        fi
        ;;

    armv7l | armv8l)
        _cputype=armv7l
        if [ "$_ostype" = "linux-android" ]; then
            _ostype=linux-androideabi
        else
            _ostype="${_ostype}eabihf"
        fi
        ;;

    aarch64)
        _cputype=aarch64
        ;;

    x86_64 | x86-64 | x64 | amd64)
        _cputype=x86_64
        ;;

    mips)
        _cputype=$(get_endianness mips '' el)
        ;;

    mips64)
        if [ "$_bitness" -eq 64 ]; then
            # only n64 ABI is supported for now
            _ostype="${_ostype}abi64"
            _cputype=$(get_endianness mips64 '' el)
        fi
        ;;

    ppc)
        _cputype=powerpc
        ;;

    ppc64)
        _cputype=powerpc64
        ;;

    ppc64le)
        _cputype=powerpc64le
        ;;

    s390x)
        _cputype=s390x
        ;;

    *)
        err "Unknown CPU type: $_cputype"
        ;;

    esac

    # Detect 64-bit linux with 32-bit userland
    if [ "${_ostype}" = unknown-linux-gnu ] && [ "${_bitness}" -eq 32 ]; then
        case $_cputype in
        x86_64)
            _cputype=i686
            ;;
        mips64)
            _cputype=$(get_endianness mips '' el)
            ;;
        powerpc64)
            _cputype=powerpc
            ;;
        aarch64)
            _cputype=armv7l
            if [ "$_ostype" = "linux-android" ]; then
                _ostype=linux-androideabi
            else
                _ostype="${_ostype}eabihf"
            fi
            ;;
        esac
    fi

    # Detect armv7 but without the CPU features Rust needs in that build,
    # and fall back to arm.
    # See https://github.com/rust-lang/rustup.rs/issues/587.
    if [ "$_ostype" = "unknown-linux-gnueabihf" ] && [ "$_cputype" = armv7l ]; then
        if ensure grep '^Features' /proc/cpuinfo | grep -q -v neon; then
            # At least one processor does not have NEON.
            _cputype=arm
        fi
    fi

    OSTYPE=$_ostype
    ARCH=$_cputype
}

# bits of this were adapted from moby check-config.shells
# See https://github.com/moby/moby/blob/master/contrib/check-config.sh
#Reference Issue : https://github.com/Azure/iotedge/issues/5812
check_cgroup_heirachy() {
    wrap_debug_message "Checking cgroup hierarchy..."
    EXITCODE=0
    if [ "$(stat -f -c %t /sys/fs/cgroup 2>/dev/null)" = '63677270' ]; then
        wrap_good 'cgroup hierarchy' 'cgroupv2'
        cgroupv2ControllerFile='/sys/fs/cgroup/cgroup.controllers'
        if [ -f "$cgroupv2ControllerFile" ]; then
            for controller in cpu cpuset io memory pids; do
                if grep -qE '(^| )'"$controller"'($| )' "$cgroupv2ControllerFile"; then
                    wrap_good "$controller" 'available'
                else
                    wrap_bad "$controller missing"
                    EXITCODE=1
                fi
            done
        else
            wrap_bad "$cgroupv2ControllerFile nonexistent??"
            EXITCODE=1
        fi
        # TODO find an efficient way to check if cgroup.freeze exists in subdir
    else
        cgroupSubsystemDir="$(sed -rne '/^[^ ]+ ([^ ]+) cgroup ([^ ]*,)?(cpu|cpuacct|cpuset|devices|freezer|memory)[, ].*$/ { s//\1/p; q }' /proc/mounts)"
        cgroupDir="$(dirname "$cgroupSubsystemDir")"
        if [ -d "$cgroupDir/cpu" ] || [ -d "$cgroupDir/cpuacct" ] || [ -d "$cgroupDir/cpuset" ] || [ -d "$cgroupDir/devices" ] || [ -d "$cgroupDir/freezer" ] || [ -d "$cgroupDir/memory" ]; then
            wrap_good "cgroup hierarchy at $cgroupDir" "properly mounted"
        else
            if [ "$cgroupSubsystemDir" ]; then
                wrap_bad "cgroup hierarchy at $cgroupSubsystemDir single mountpoint!"
            else
                wrap_bad "cgroup hierarchy nonexistent??"
            fi
            EXITCODE=1
            wrap_warning_message "See https://github.com/tianon/cgroupfs-mount"
        fi
    fi

    if [ $EXITCODE -eq 0 ]; then
        wrap_pass "check_cgroup_heirachy"
    else
        wrap_fail "check_cgroup_heirachy"
    fi
}

check_kernel_flags() {
    wrap_debug_message "Checking Kernel Flags..."
    EXIT_CODE=0
    check_kernel_flags_file_util
    if [ $EXIT_CODE != 0 ]; then
        wrap_fail "check_kernel_flags"
        wrap_bad "Try running this script again by specifying the kernel config as CONFIG=/path/to/kernel/.config $0"
        return
    fi
    EXIT_CODE=0
    wrap_debug_message "Reading Kernel Config from $CONFIG"
    for flag in "$@"; do
        printf -- '- '
        check_kernel_flags_util "$flag"
    done
}

check_kernel_flags_file_util() {
    if [ ! -e "$CONFIG" ]; then
        wrap_debug_message "CONFIG env variable does not exist, searching other paths for kernel config"
        for tryConfig in $POSSIBLE_CONFIGS; do
            if [ -e "$tryConfig" ]; then
                CONFIG="$tryConfig"
                break
            fi
        done
        if [ ! -e "$CONFIG" ]; then
            wrap_debug_message "Cannot find kernel config in other paths"
            EXIT_CODE=1
        fi
    fi
}

check_kernel_flags_util() {
    if is_set "$1"; then
        wrap_pass "CONFIG_$1"
    else
        wrap_fail "CONFIG_$1"
    fi
}

check_systemd() {
    wrap_debug_message "Checking systemd presence..."
    if [ -z "$(pidof systemd)" ]; then
        wrap_warning "check_systemd"
        wrap_warning_message "Systemd is not present on this device. Iot Edge services must be managed independently. For running azure iot edge without systemd, visit: https://github.com/Azure/iotedge/blob/master/edgelet/doc/devguide.md#run"
    else
        wrap_pass "check_systemd"
    fi
}

check_architecture() {
    wrap_debug_message "Checking architecture Compatibility..."
    wrap_debug_message "Architecture:$ARCH"

    case $ARCH in
    x86_64 | armv7l | aarch64)
        wrap_pass "check_architecture"
        ;;
    arm)
        wrap_fail "check_architecture"
        wrap_bad "armv6 architecture is incompatible with IoT Edge due to .NET incompatibility. Please see" "https://github.com/dotnet/runtime/issues/7764"
        ;;
    *)
        wrap_warning "check_architecture"
        wrap_warning_message "Compatibility for IoT Edge not known for architecture $ARCH"
        ;;
    esac
}

#Todo : This will need to be checked here : https://github.com/Azure/iotedge/blob/main/edgelet/docker-rs/src/apis/configuration.rs#L14 for every build
#to make sure we still support the version.

check_docker_api_version() {
    # Check dependencies
    wrap_debug_message "Checking docker api client version..."

    #TODO : This is how we check  for a container engine in our packages. Is this the right way?
    ret=$(need_cmd docker)
    if [ "$?" -ne 0 ]; then
        wrap_warning "check_docker_api_version"
        wrap_warning_message "Docker Engine does not exist on this device, Please follow instructions here on how to install a compatible container engine
        https://docs.microsoft.com/en-us/azure/iot-edge/how-to-provision-single-device-linux-symmetric?view=iotedge-2020-11&tabs=azure-portal%2Cubuntu#install-a-container-engine"
        return
    fi

    version=$(docker version -f '{{.Client.APIVersion}}')
    if [ $? != 0 ]; then
        wrap_warning "check_docker_api_version"
        wrap_warning_message "Could not get Docker Version"
        return
    fi

    wrap_debug_message "Docker API Version is $version, Minimum Docker Version Required is $1"
    version_check=$(echo "$version" $1 | awk '{if ($1 < $2) print 1; else print 0}')
    if [ "$version_check" -eq 0 ]; then
        wrap_pass "check_docker_api_version"
    else
        wrap_fail "check_docker_api_version"
        wrap_warning_message "Docker API Version on device $version is lower than Minumum API Version $MINIMUM_DOCKER_API_VERSION. Please upgrade docker engine."
    fi
}

check_shared_library_dependency() {
    wrap_debug_message "Checking shared library dependency..."
    if [ "$ARCH" = x86_64 ]; then
        SHARED_LIBRARIES="$(echo $SHARED_LIBRARIES_BASE $CURRENT_SHARED_LIBRARIES_x86_64)"
    elif [ "$ARCH" = aarch64 ]; then
        SHARED_LIBRARIES="$(echo $SHARED_LIBRARIES_BASE $CURRENT_SHARED_LIBRARIES_aarch64)"
    elif [ "$ARCH" = armv7l ]; then
        SHARED_LIBRARIES="$(echo $SHARED_LIBRARIES_BASE $CURRENT_SHARED_LIBRARIES_armv7l)"
    fi

    wrap_debug_message "Shared libraries to check: $SHARED_LIBRARIES"

    for lib in $SHARED_LIBRARIES; do
        check_ldconfig=$(need_cmd ldconfig)
        result_ldconfig="$?"
        # case with root privilege and ldconfig exists
        if [ "$(id -u)" -eq 0 ] && [ "$result_ldconfig" -eq 0 ] ; then
            check_shared_lib_ldconfig_util "$lib"
            result_lib="$?"
            if [ "$result_lib" -ne 0 ] && [ "$lib" = "$SHARED_LIB_LIBSSSL_1_1" ]; then
                check_shared_lib_ldconfig_util "$SHARED_LIB_LIBSSSL_1_0"
            fi
        else
            check_shared_lib_find_util "$lib"
            result_lib="$?"
            if [ "$result_lib" -ne 0 ] && [ "$lib" = "$SHARED_LIB_LIBSSSL_1_1" ]; then
                check_shared_lib_find_util "$SHARED_LIB_LIBSSSL_1_0"
            fi
        fi
    done
}

check_shared_lib_find_util() {
    found_library=0
    for path in $SHARED_LIB_PATH; do
        if [ ! -e "$path" ]; then
            wrap_warning_message "Path : $path does not exist, Searching for other paths"
            continue
        else
            if [ ! "$(find "$path" -name "$1" | grep .)" ]; then
                found_library=0
            else
                found_library=1
                break
            fi
        fi
    done
    if [ $found_library -eq 0 ]; then
        check_shared_lib_display_warning "$1" "library_$1"
        return 1
    else
        wrap_pass "library_$1"
        return 0
    fi
}

check_shared_lib_ldconfig_util() {
    ret=$(ldconfig -p | grep "$1")
    if [ -z "$ret" ]; then
        check_shared_lib_display_warning "$1" "library_$1"
        return 1
    else
        wrap_pass "library_$1"
        return 0
    fi
}

check_shared_lib_display_warning() {
    wrap_warning_message "Cannot find Library $1 in $SHARED_LIB_PATH"
    wrap_warning_message "Try running this script again, providing the shared library path for your distro"
    wrap_warning_message "SHARED_LIB_PATH=/path/to/shared_lib $0"
    case $1 in
    "libssl.so.1.1" | "libssl.so.1.0" | "libcrypto.so.1.1")
        wrap_fail "$2"
        wrap_warning_message "If problem still persists, please install openssl and libssl-dev for your OS distribution."
        ;;
    "libdl.so.2" | "librt.so.1" | "libpthread.so.0" | "libc.so.6" | "libm.so.6")
        wrap_fail "$2"
        wrap_warning_message "If problem still persists, please install libc6-dev for your OS distribution."
        ;;
    "ld-linux-x86-64.so.2" | "ld-linux-aarch64.so.1" | "ld-linux-armhf.so.3")
        wrap_fail "$2"
        wrap_warning_message "If problem still persists, please install libc6 for your OS distribution."
        ;;
    "libgcc_s.so.1")
        wrap_fail "$2"
        wrap_warning_message "If problem still persists, please install gcc for your OS distribution."
        ;;
    esac
}

check_storage_space() {
    wrap_debug_message "Checking storage space..."
    eval binary_size='$'"$(echo "$ARCH"_iotedge_binaries_size)"
    eval container_size='$'"$(echo "$ARCH"_iotedge_container_size)"

    # Round Numbers
    binary_size=$(echo $binary_size | awk '{printf "%.0f", $1}')
    container_size=$(echo $container_size | awk '{printf "%.0f", $1}')
    TOTAL_SIZE=$(echo $binary_size $container_size | awk '{print $1 + $2}')

    if [ -z "$MOUNTPOINT" ]; then
        MOUNTPOINT=$(pwd)
        wrap_debug_message "The Mountpoint where application is intented to be installed is unknown, using $MOUNTPOINT"
    fi

    check_storage_space_util "$MOUNTPOINT" "$TOTAL_SIZE" "$iotedge_size_buffer"
    ret="$?"

    wrap_debug_message "IoT Edge requires a minimum storage space of $((container_size + binary_size + iotedge_size_buffer)) MB for installing Edge."
    wrap_debug_message "The device has $available_storage MB of available storage for File System $(df -P -m "$MOUNTPOINT" | awk '{print $6}')"

    if [ $ret -eq 0 ]; then
        #TODO : Check with PM on messaging
        wrap_warning_message "!! Additional storage may be required based on usage. Please visit aka.ms/iotedge for more information"
    elif [ $ret -eq 1 ]; then
        wrap_warning_message "If you are planning to install iotedge on a different mountpoint, please run the script with MOUNTPOINT='<Path-to-mount>' $(basename "$0")"
    fi
}

# Takes in a File System path , Size of Application in MB and Buffer Size in MB
check_storage_space_util() {
    storage_path=$1
    application_size=$2
    buffer=$3

    # Check dependencies
    ret=$(need_cmd df)
    if [ "$?" -ne 0 ]; then
        wrap_debug_message "Could not find df utility to calculate disk space, Skipping the check"
        wrap_skip "check_storage_space"
        return 2
    fi

    if ! echo "$application_size" | grep -Eq '^[0-9]+\.?[0-9]+$'; then
        wrap_bad "Invalid Application Size provided $application_size"
        wrap_fail "check_storage_space"
        return 2
    fi

    #Provide a Buffer of $buffer to account for any additional log files and storage.
    application_size=$(echo "$application_size" "$buffer" | awk '{print $1 + $2}')
    wrap_debug_message "Application Size is $application_size MB"

    # Print storage space in posix, Output is in Kb, conver to MB since our storage calculations are in MB
    available_storage=$(df -P "$storage_path" | awk '{print $4}' | sed "s/[^0-9]//g" | tr -d '\n' | awk '{print $1/1024}')
    adequate_storage=$(echo "$available_storage" "$application_size" | awk '{if ($1 > $2) print 1; else print 0}')

    if [ "$adequate_storage" -eq 1 ]; then
        wrap_pass "check_storage_space"
        return 0
    else
        wrap_fail "check_storage_space"
        return 1
    fi
}

check_package_manager() {
    wrap_debug_message "Checking package managers..."
    not_found=0
    skip_ca_cert=0
    package_managers="apt-get dnf yum dpkg rpm"
    for package in $package_managers; do
        # TODO : Is there a better way to do this?
        res="$(need_cmd $package)"
        if [ $? -eq 0 ]; then
            not_found=0
            wrap_debug_message "Current target platform supports $package package manager"
            wrap_pass "check_package_manager"
            if [ $package = "rpm" ] || [ $package = "dpkg" ]; then
                skip_ca_cert=1
                check_ca_cert
            fi
            break
        else
            not_found=1
            wrap_debug_message "Current target platform does not support $package package manager"
        fi
    done
    if [ "$not_found" -eq 1 ]; then
        #TODO: update the link
        wrap_warning "check_package_manager"
        wrap_warning_message "IoT Edge supports [*deb, *rpm] package types and [apt-get] package manager."
        wrap_warning_message "Platform does not have support for the supported package type. Please head to aka.ms/iotedge for instructions on how to build the iotedge binaries from source"
        skip_ca_cert=1
        check_ca_cert
    fi
    if [ ! "$skip_ca_cert" -eq 1 ]; then
        wrap_skip "check_ca_cert"
    fi
}

check_ca_cert() {
    wrap_debug_message "Checking ca-certificates package..."
    find_openssl=$(openssl version >/dev/null 2>&1)
    if [ "$?" -eq 0 ]; then
        ca_cert_dir=$(openssl version -d | awk '{print $2}'| sed "s/\"//g" | tr -d " ")"/certs"
        wrap_debug_message "CA: cert directory $ca_cert_dir"
        # Check first if the directory exists.
        if [ ! -d "$ca_cert_dir" ]; then
            wrap_warning "check_ca_cert"
            wrap_warning_message "Could not find ca-certificates. It is required for TLS Communication with IoT Hub"
        else
            # Check if the directory has cert files
            find_crt=$(find -L "$ca_cert_dir" -type f -name "*.pem" -o -name "*.crt" | grep .)
            if [ "$?" -ne 0 ]; then
                wrap_warning "check_ca_cert"
                wrap_warning_message "Could not find ca-certificates at $ca_cert_dir. It is required for TLS Communication with IoT Hub"
            else
                wrap_pass "check_ca_cert"
            fi
        fi
    else
        wrap_debug_message "OpenSSL is not installed"
        wrap_skip "check_ca_cert"
    fi
}

check_free_memory() {
    wrap_debug_message "Checking memory space..."
    memory_filename="/proc/meminfo"
    if [  ! -f "$memory_filename" ] ; then
        wrap_skip "check_free_memory"
        return 2
    fi
    eval iotedge_binary_memory='$'"$(echo "$ARCH"_iotedge_binaries_avg_memory)"
    eval iotedge_container_memory='$'"$(echo "$ARCH"_iotedge_container_memory)"

    #Round Numbers
    iotedge_binary_memory=$(echo "$iotedge_binary_memory" | awk '{printf "%.0f", $1}')
    iotedge_container_memory=$(echo "$iotedge_container_memory" | awk '{printf "%.0f", $1}')

    total_iotedge_memory_size=$(echo $iotedge_binary_memory $iotedge_container_memory $iotedge_memory_buffer | awk '{print $1 + $2 + $3}')

    # /proc/meminfo returns the memory size in KB, but our memory calculations are in MB, convert it to appropriate units
    current_free_memory=$(cat $memory_filename | grep "MemAvailable" | awk '{print $2/1024}')

    #TODO: correct final link of aka.ms/iotedge with the setup info of memory analysis.
    wrap_debug_message "IoT Edge requires a minimum memory of $total_iotedge_memory_size MB for a simple workload"

    res=$(echo $current_free_memory $total_iotedge_memory_size | awk '{if ($1 > $2) print 1; else print 0}')
    if [ $res -eq 1 ]; then
        wrap_debug_message "The device has $current_free_memory MB of free memory"
        wrap_pass "check_free_memory"
        wrap_warning_message "!! Additional memory may be required based on usage. Please visit aka.ms/iotedge for more information"
    else
        # TODO: Need to refine this message
        wrap_fail "check_free_memory"
        wrap_warning_message "Current available memory is $current_free_memory MB. Free up atleast $total_iotedge_memory_size MB to run IoT Edge"
    fi
}

aziotedge_check() {

    #Todo : As we add new versions, these checks will need to be changed. Keep a common check for now
    case $APP_VERSION in
    *) wrap_debug_message "Checking aziot-edge compatibility for Release 1.2" ;;
    esac

    #Required for resource allocation for containers
    check_cgroup_heirachy

    #Flags Required for setting elevated capabilities in a container. EdgeHub currently requires setting CAP_NET_BIND on dotnet binary:EXT4_FS_SECURITY
    #Kernel flags required for running a container engine. For description on each of the config flags : Visit -https://www.kernelconfig.io/
    #Todo : Only check if docker engine is not present?
    #Check for Required Container Engine Flags if docker is not present
    check_kernel_flags \
        EXT4_FS_SECURITY \
        NAMESPACES NET_NS PID_NS IPC_NS UTS_NS \
        CGROUPS CGROUP_CPUACCT CGROUP_DEVICE CGROUP_FREEZER CGROUP_SCHED CPUSETS MEMCG \
        KEYS \
        VETH BRIDGE BRIDGE_NETFILTER \
        IP_NF_FILTER IP_NF_TARGET_MASQUERADE \
        NETFILTER_XT_MATCH_ADDRTYPE \
        NETFILTER_XT_MATCH_CONNTRACK \
        NETFILTER_XT_MATCH_IPVS \
        NETFILTER_XT_MARK \
        IP_NF_NAT NF_NAT \
        POSIX_MQUEUE
    #(POSIX_MQUEUE is required for bind-mounting /dev/mqueue into containers)

    check_systemd
    check_architecture
    check_docker_api_version $MINIMUM_DOCKER_API_VERSION
    check_shared_library_dependency
    check_storage_space
    check_package_manager
    check_free_memory
}

list_apps() {
    #Add Supported Applications here - Space delimited
    echo "aziotedge"
}

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage() {
    echo "$(basename "$0") [options]"
    echo ""
    echo "options"
    echo " -a, --app-name              The name of the application to check for compatibility. Execute \"$(basename "$0")\" -l to get list of supported applications"
    echo "     --app-version           The version of the application to check compatibility for. Defaults to latest version of the application."
    echo " -l, --list-apps             List of application checks supported by the compatibility script"
    echo " -h, --help                  Print this help and exit."
    echo " -v, --verbose               Shows Verbose Logs"
    exit 1
}

process_args() {
    save_next_arg=0
    for arg in "$@"; do
        if [ $save_next_arg -eq 1 ]; then
            APP_NAME=$arg
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            APP_VERSION=$arg
            save_next_arg=0
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "-a" | "--app-name") save_next_arg=1 ;;
            "--app-version") save_next_arg=2 ;;
            "-v" | "--verbose") VERBOSE=1 ;;
            "-l" | "--list-apps") list_apps && exit 0 ;;
            *) usage ;;
            esac
        fi
    done
}
echo "--------------------"
echo "Compatibility Checks"
echo "--------------------"

if [ "$(id -u)" -ne 0 ]; then
    wrap_warning "Platform Compatibility Tool is not running as root"
else
    wrap_pass "Platform Compatibility Tool has root privileges"
fi
process_args "$@"
get_architecture
wrap_debug_message "$(cat /etc/os-release)"
if [ -z "$APP_NAME" ]; then
    wrap_debug_message "No Application Name Provided, Performing Check on all supported Applications"
    for app in $(list_apps); do
        wrap_debug_message "Performing Compatibility Check for Application: $app"
        "$app"_check
    done
else
    if [ -z "$(list_apps | grep "$APP_NAME")" ]; then
        wrap_bad "Application $APP_NAME does not exist in Supported Applications $(list_apps)"
        exit 1
    fi
    "$APP_NAME"_check
fi

echo "----------------------------"
echo "Compatibility Check Results"
echo "----------------------------"
pass="$PASS check(s) succeeded"
fail="$FAILURES check(s) failed"
warn="$WARNINGS check(s) have warnings"
skip="$SKIP check(s) skipped"
echo "$(wrap_color "$pass" green)"
echo "$(wrap_color "$fail" red)"
echo "$(wrap_color "$warn" yellow)"
echo "$(wrap_color "$skip" white)"