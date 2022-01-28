#!/usr/bin/env sh

###############################################################################
# This script checks whether IoT Edge Runtime can run on a target OS
###############################################################################

#Variables
OSTYPE=""
ARCH=""

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

wrap_debug(){
    echo "$(wrap_color "$1" white)"
}

wrap_pass() {
	echo "$(wrap_color "$1" white) $(wrap_color "$2" green)"
}
wrap_fail() {
	echo "$(wrap_color "$1" bold) $(wrap_color "$2" bold red)"
}
wrap_warning() {
	wrap_color >&2 "$*" red
}


# ------------------------------------------------------------------------------
#  Retrieve OS TYPE AND ARCHITECTURE (Required for Getting IoT)
#  Derived from https://sh.rustup.rs 
# ------------------------------------------------------------------------------
need_cmd() {
    if ! check_cmd "$1"; then
     wrap_warning "need '$1' (command not found)"
     exit 1
    fi
}

check_cmd() {
    command -v "$1" > /dev/null 2>&1
}

get_gnu_musl_glibc() {
  need_cmd ldd
  need_cmd awk
  # Detect both gnu and musl
  # Also detect glibc versions older than 2.18 and return musl for these
  # Required until we identify minimum supported version
  # TODO: https://github.com/vectordotdev/vector/issues/10807
  local _ldd_version
  local _glibc_version
  _ldd_version=$(ldd --version 2>&1)
  if [ -z "${_ldd_version##*GNU*}" ] || [ -z "${_ldd_version##*GLIBC*}" ]; then
    _glibc_version=$(echo "$_ldd_version" | awk '/ldd/{print $NF}')
    version_check=$(echo $_glibc_version 2.18 | awk '{if ($1 < $2) print 1; else print 0}')
    if [ version_check -eq 1 ]; then
      wrap_debug "musl"
    else
      wrap_debug "gnu"
    fi
  elif [ -z "${_ldd_version##*musl*}" ]; then
    wrap_debug "musl"
  else
    wrap_debug "Unknown architecture from ldd: ${_ldd_version}"
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
    _current_exe_head=$(head -c 5 /proc/self/exe )
    if [ "$_current_exe_head" = "$(printf '\177ELF\001')" ]; then
        echo 32
    elif [ "$_current_exe_head" = "$(printf '\177ELF\002')" ]; then
        echo 64
    else
        err "unknown platform bitness"
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
        err "unknown platform endianness"
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
            case $(get_gnu_musl_glibc) in
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
            err "unknown CPU type: $_cputype"

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

perform_cleanup(){
    rm -rf cap.txt || true
    #TODO : Cleanup docker images
}


# ------------------------------------------------------------------------------
# Check whether the Target Device can be used to set capability. EdgeHub Runtime component sets CAP_NET_BIND which is Required
# for Azure IoT Edge Operation
# ------------------------------------------------------------------------------
perform_capability_check_host(){
    
    wrap_debug "Checking Set Cap Capability on Host.."
    touch cap.txt
    setcap "cap_net_bind_service=+ep" cap.txt
    if [ $? != 0 ]; then
        #TODO Check Mark Failed in Red
        wrap_debug "Failed to Set Capability on Host Container"
        wrap_fail "Capability_Check_Host" "Fail"
        return
    fi

    contains=$(getcap cap.txt | grep 'cap_net_bind_service+ep')
    if [ $? != 0 ] && [ -z "${contains##*cap_net_bind_service+ep*}" ]; then
        wrap_debug "Failed to Verify Set Capability on Host Container"
        wrap_fail "Capability_Check_Host" "Fail"
        return
    fi

    wrap_pass "Capability_Check_Host" "Pass"
}

perform_capability_check_container(){
    #Check For Docker
    wrap_debug "Checking Set Cap Capability on Container.."
    need_cmd docker
    CAP_CMD="getcap cap.txt"
    DOCKER_VOLUME_MOUNTS=''
    DOCKER_IMAGE="ubuntu:18.04"
    #TODO: Use different images based on platform and arch
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
        if [ $? != 0 ]; then
            exit 1
        fi
    "
    if [ $? != 0 ]; then
        #TODO Check Mark Failed in Red
        wrap_debug "Failed to Check Capability in Container, Check Failed"
        wrap_fail "Capability_Check_Container" "Fail"
        return
    fi
    wrap_pass "Capability_Check_Container" "Pass"
}


echo "Running IoT Edge Compatibility Tool"
get_architecture || exit 1
echo "Architecture:$ARCH"
echo "OS Type:$OSTYPE"

#TODO : Do we need both?
perform_capability_check_host
perform_capability_check_container
perform_cleanup
echo "IoT Edge Compatibility Tool Check Complete"



##TODO: Implement Below Checks/Functions

## System Identification
#Function to find out System Memory and Storage Capacity
#Function to find out Libraries present in the Target OS

#IoT Edge System Inputs
# Target Memory and CPU (Varies by Release/ARCH/OS)
# Dependent Libraries(Varies by Release/ARCH/OS)

## Checks
#Fails on The Following Checks
#Run Moby Config Checks as part of the script and ensure configs present to install docker (Maybe Separate run)
#Compare IoTEdge Daemon , IIS Shared Libraries with Target OS
#compare Memory and CPU usage (Varies for each ARCH/OS)

##Warnings
# Check if Compatible Package Manager is installed
# Check if Docker is present or not

