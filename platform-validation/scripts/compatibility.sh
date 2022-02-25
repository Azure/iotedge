#!/usr/bin/env sh

###############################################################################
# This script checks whether IoT Edge can run on a target OS
###############################################################################

#Variables
OSTYPE=""
ARCH=""

# ------------------------------------------------------------------------------
#  Text Formatting
#  Derived from : https://github.com/moby/moby/blob/master/contrib/check-config.sh
# ------------------------------------------------------------------------------

POSSIBLE_CONFIGS="
	/proc/config.gz
	/boot/config-$(uname -r)
	/usr/src/linux-$(uname -r)/.config
	/usr/src/linux/.config
"

if [ $# -gt 0 ]; then
    CONFIG="$1"
else
    : "${CONFIG:=/proc/config.gz}"
fi

if ! command -v zgrep >/dev/null 2>&1; then
    zgrep() {
        zcat "$2" | grep "$1"
    }
fi

is_set() {
    zgrep "CONFIG_$1=[y|m]" "$CONFIG" >/dev/null
}

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
    echo "$(wrap_color "$1" white): $(wrap_color "$2" green)"
}
wrap_bad() {
    echo "$(wrap_color "$1" bold): $(wrap_color "$2" bold red)"
}

wrap_debug() {
    echo "$(wrap_color "$1" white)"
}

wrap_pass() {
    echo "$(wrap_color "$1 - OK" green)"
}
wrap_fail() {
    echo "$(wrap_color "$1 - Error" bold red)"
}

wrap_warn() {
    echo "$(wrap_color "$1 - Warning!!" yellow)"
}

wrap_warning() {
    wrap_color >&2 "$*" yellow
}

# ------------------------------------------------------------------------------
#  Retrieve OS TYPE AND ARCHITECTURE (Required for Getting IoT)
#  Derived from https://sh.rustup.rs
# ------------------------------------------------------------------------------
need_cmd() {
    if ! check_cmd "$1"; then
        wrap_warning "'$1' (command not found)"
        exit 1
    fi
}

check_cmd() {
    command -v "$1" >/dev/null 2>&1
}

get_libc() {
    need_cmd ldd
    need_cmd awk
    # Detect both gnu and musl
    # Also detect glibc versions older than 2.18 and return musl for these
    # Required until we identify minimum supported version
    # TODO: https://github.com/vectordotdev/vector/issues/10807
    local _ldd_version
    local _libc_version
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
    local _current_exe_head
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
    local cputype=$1
    local suffix_eb=$2
    local suffix_el=$3

    # detect endianness without od/hexdump, like get_bitness() does.
    need_cmd head
    need_cmd tail

    local _current_exe_endianness
    _current_exe_endianness="$(head -c 6 /proc/self/exe | tail -c 1)"
    if [ "$_current_exe_endianness" = "$(printf '\001')" ]; then
        echo "${cputype}${suffix_el}"
    elif [ "$_current_exe_endianness" = "$(printf '\002')" ]; then
        echo "${cputype}${suffix_eb}"
    else
        wrap_err "Unknown platform endianness"
    fi
}

get_architecture() {
    local _ostype _cputype _bitness _arch
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
        _cputype=armv7
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
            _cputype=armv7
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
    if [ "$_ostype" = "unknown-linux-gnueabihf" ] && [ "$_cputype" = armv7 ]; then
        if ensure grep '^Features' /proc/cpuinfo | grep -q -v neon; then
            # At least one processor does not have NEON.
            _cputype=arm
        fi
    fi

    OSTYPE=$_ostype
    ARCH=$_cputype
}

perform_cleanup() {
    rm -rf cap.txt || true
    #TODO : Cleanup docker images
}

# ------------------------------------------------------------------------------
# Check whether the Target Device can be used to set capability. EdgeHub   Runtime component sets CAP_NET_BIND which is Required for Azure IoT Edge Operation.
# ------------------------------------------------------------------------------

check_net_cap_bind_host() {
    wrap_debug "Setting the CAP_NET_BIND_SERVICE capability on the host..."

    # Check dependencies
    ret=$(need_cmd setcap)
    if [ $? != 0 ]; then
        wrap_fail "check_net_cap_bind_host"
        return
    fi

    touch cap.txt
    setcap "cap_net_bind_service=+ep" cap.txt
    ret=$?
    if [ $ret != 0 ]; then
        wrap_debug "setcap 'cap_net_bind_service=+ep' returned $ret"
        wrap_fail "check_net_cap_bind_host"
        return
    fi

    contains=$(getcap cap.txt | grep 'cap_net_bind_service+ep')
    ret=$?
    if [ $? != 0 ] && [ -z "${contains##*cap_net_bind_service+ep*}" ]; then
        wrap_debug "setcap 'cap_net_bind_service=+ep' returned 0, but did not set the capability"
        wrap_fail "check_net_cap_bind_host"
        return
    fi

    wrap_pass "check_net_cap_bind_host" "Pass"
}

check_net_cap_bind_container() {
    #Check For Docker
    wrap_debug "Setting the CAP_NET_BIND_SERVICE capability in a container..."

    # Check dependencies
    ret=$(need_cmd docker)
    if [ $? != 0 ]; then
        wrap_fail "check_net_cap_bind_container" "Fail"
        return
    fi
    CAP_CMD="getcap cap.txt"
    DOCKER_VOLUME_MOUNTS=''
    #Todo: Look into replacing this with alpine
    DOCKER_IMAGE="ubuntu:18.04"
    docker run --rm \
        --user root \
        -e 'USER=root' \
        -i \
        $DOCKER_VOLUME_MOUNTS \
        "$DOCKER_IMAGE" \
        sh -c "
        export DEBIAN_FRONTEND=noninteractive
        set -e &&
        apt-get update 1>/dev/null 2>&1 &&
        apt-get install -y libcap2-bin 1>/dev/null 2>&1
        touch cap.txt
        setcap 'cap_net_bind_service=+ep' cap.txt
    "
    ret=$?
    if [ $ret != 0 ]; then
        wrap_debug "setcap 'cap_net_bind_service=+ep' on container returned error code $ret"
        wrap_fail "check_net_cap_bind_container" "Fail"
        return
    fi
    wrap_pass "check_net_cap_bind_container" "Pass"
}

# bits of this were adapted from moby check-config.shells
# See https://github.com/moby/moby/blob/master/contrib/check-config.sh

#Reference Issue : https://github.com/Azure/iotedge/issues/5812
check_cgroup_heirachy() {
    wrap_debug "Checking cgroup hierarchy..."
    EXITCODE=0
    if [ "$(stat -f -c %t /sys/fs/cgroup 2>/dev/null)" = '63677270' ]; then
        wrap_good 'cgroup hierarchy' 'cgroupv2'
        cgroupv2ControllerFile='/sys/fs/cgroup/cgroup.controllers'
        if [ -f "$cgroupv2ControllerFile" ]; then
            echo '  Controllers:'
            for controller in cpu cpuset io memory pids; do
                if grep -qE '(^| )'"$controller"'($| )' "$cgroupv2ControllerFile"; then
                    echo "  - $(wrap_good "$controller" 'available')"
                else
                    echo "  - $(wrap_bad "$controller" 'missing')"
                    EXITCODE=1
                fi
            done
        else
            wrap_bad "$cgroupv2ControllerFile" 'nonexistent??'
            EXITCODE=1
        fi
        # TODO find an efficient way to check if cgroup.freeze exists in subdir
    else
        cgroupSubsystemDir="$(sed -rne '/^[^ ]+ ([^ ]+) cgroup ([^ ]*,)?(cpu|cpuacct|cpuset|devices|freezer|memory)[, ].*$/ { s//\1/p; q }' /proc/mounts)"
        cgroupDir="$(dirname "$cgroupSubsystemDir")"
        if [ -d "$cgroupDir/cpu" ] || [ -d "$cgroupDir/cpuacct" ] || [ -d "$cgroupDir/cpuset" ] || [ -d "$cgroupDir/devices" ] || [ -d "$cgroupDir/freezer" ] || [ -d "$cgroupDir/memory" ]; then
            echo "$(wrap_good 'cgroup hierarchy' 'properly mounted') [$cgroupDir]"
        else
            if [ "$cgroupSubsystemDir" ]; then
                echo "$(wrap_bad 'cgroup hierarchy' 'single mountpoint!') [$cgroupSubsystemDir]"
            else
                wrap_bad 'cgroup hierarchy' 'nonexistent??'
            fi
            EXITCODE=1
            echo "    $(wrap_color '(see https://github.com/tianon/cgroupfs-mount)' yellow)"
        fi
    fi

    if [ $EXITCODE -eq 0 ]; then
        wrap_pass "check_cgroup_heirachy"
    else
        wrap_fail "check_cgroup_heirachy"
    fi
}

check_systemd() {
    wrap_debug "Checking presence of systemd..."
    if [ -z "$(pidof systemd)" ]; then
        wrap_warn "check_systemd"
        wrap_debug "Systemd is not present on this device, As a result azure iot edge services will need to be run and managed independently.
        For instructions on running azure iot edge without systemd, visit: https://github.com/Azure/iotedge/blob/master/edgelet/doc/devguide.md#run"
    else
        wrap_pass "check_systemd"
    fi
}

check_architecture() {

    wrap_debug "Checking architecture Compatibility..."
    get_architecture
    wrap_debug "Architecture:$ARCH"

    case $ARCH in
    x86_64 | armv7 | aarch64)
        wrap_pass "check_architecture"
        ;;
    arm)
        wrap_fail "check_architecture"
        wrap_bad "armv6 architecture is incompatible with IoT Edge due to .NET incompatibility. Please see : https://github.com/dotnet/runtime/issues/7764"
        ;;
    *)
        wrap_warning "check_architecture"
        wrap_warn "Compatibility for IoT Edge not known for architecture $ARCH"
        ;;
    esac

}

#Todo : This will need to be checked here : https://github.com/Azure/iotedge/blob/main/edgelet/docker-rs/src/apis/configuration.rs#L14 for every build
#to make sure we still support the version.
MINIMUM_DOCKER_API_VERSION=1.34
check_docker_api_version(){
    # Check dependencies
    if ! need_cmd docker; then
        wrap_warning "check_docker_api_version"
        wrap_warn "Docker Enginer does not exist on this device!!, Please follow instructions here on how to install a compatible container engine
        https://docs.microsoft.com/en-us/azure/iot-edge/how-to-provision-single-device-linux-symmetric?view=iotedge-2020-11&tabs=azure-portal%2Cubuntu#install-a-container-engine"
        return
    fi

    version=$(docker version -f '{{.Client.APIVersion}}')
    version_check=$(echo "$version" $MINIMUM_DOCKER_API_VERSION | awk '{if ($1 < $2) print 1; else print 0}')
    if [ "$version_check" -eq 0 ]; then
        wrap_pass "check_docker_api_version"
    else
        wrap_fail "check_docker_api_version"
        wrap_warning "Docker API Version on device $version is lower than Minumum API Version $MINIMUM_DOCKER_API_VERSION. Please upgrade docker engine."
    fi

}

check_shared_library_dependency(){
    wrap_debug "Checking shared library dependency for aziot-edged and aziot-identityd"

    # set the crucial libaries in the variable
    # IOTEDGE_COMMON_SHARED_LIBRARIES is for both aziot-edged and aziot-identityd
    IOTEDGE_COMMON_LIBRARIES="libssl.so.1.1 libcrypto.so.1.1 libdl.so.2 librt.so.1 libpthread.so.0 libc.so.6 libm.so.6 libgcc_s.so.1"
    for lib in $IOTEDGE_COMMON_LIBRARIES
    do
        check_shared_library_dependency_core_util "$lib"
    done

    if [ $ARCH = x86_64 ]; then
        check_shared_library_dependency_core_util "ld-linux-x86-64.so.2"
    elif [ $ARCH = aarch64 ]; then
        check_shared_library_dependency_core_util "ld-linux-aarch64.so.1"
    elif [ $ARCH = armv7 ]; then
        check_shared_library_dependency_core_util "ld-linux-armhf.so.3"
    fi
}

check_shared_library_dependency_core_util(){

    # setting share library path
    if [ $# -gt 1 ]; then
        SHARED_LIB_PATH="$2"
    else
        SHARED_LIB_PATH="/usr /lib /lib32 /lib64"
    fi

    # check dependencies for `ldconfig` and fall back to `find` when its not possible
    if [ "$(id -u)" -ne 0 ] && [ "$(need_cmd ldconfig)" != 0 ]; then
        decision=0
        for path in $SHARED_LIB_PATH
        do
            if [ ! "$(find "$path" -name "$1" | grep .)" ]; then
                decision=$((decision + 1))
            else
                wrap_pass "$1"
                break
            fi
        done
        # if the libraries are not present in all 4 paths, it is considered to be missing.
        if [ $decision -eq 4 ]; then
            check_shared_library_dependency_display_util "$1"
        fi
        return
    else
        ret=$(ldconfig -p | grep "$1")
        if [ -z "$ret" ]; then
            check_shared_library_dependency_display_util "$1"
        else
            wrap_pass "$1"
        fi
    fi
}

check_shared_library_dependency_display_util(){
    case $1 in
        "libssl.so.1.1" | "libcrypto.so.1.1") wrap_warning "$lib is missing. Please install openssl and libssl-dev for your OS distribution."
        ;;
        "libdl.so.2"| "librt.so.1" | "libpthread.so.0" | "libc.so.6" | "libm.so.6") wrap_warning "$lib is missing. Please install libc6-dev for your OS distribution."
        ;;
        "ld-linux-x86-64.so.2" | "ld-linux-aarch64.so.1" | "ld-linux-armhf.so.3" ) wrap_warning "$lib is missing. Please install libc6 for your OS distribution."
        ;;
        "libgcc_s.so.1") wrap_warning "$lib is missing. Please install gcc for your OS distribution."
        ;;
    esac
}

#TODO : Do we need to check in both host and container?
check_net_cap_bind_host
check_net_cap_bind_container
check_cgroup_heirachy
check_systemd
check_architecture
check_docker_api_version
check_shared_library_dependency
perform_cleanup
echo "IoT Edge Compatibility Tool Check Complete"
