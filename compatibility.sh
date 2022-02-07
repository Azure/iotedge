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

possibleConfigs="
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

if ! command -v zgrep > /dev/null 2>&1; then
	zgrep() {
		zcat "$2" | grep "$1"
	}
fi

is_set() {
	zgrep "CONFIG_$1=[y|m]" "$CONFIG" > /dev/null
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

wrap_debug(){
    echo "$(wrap_color "$1" white)"
}

wrap_pass() {
	echo "$(wrap_color "$1 - OK" green)"
}
wrap_fail() {
	echo "$(wrap_color "$1 - Error" bold red)"
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
    command -v "$1" > /dev/null 2>&1
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
    _current_exe_head=$(head -c 5 /proc/self/exe )
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
# Check whether the Target Device can be used to set capability. EdgeHub   Runtime component sets CAP_NET_BIND which is Required for Azure IoT Edge Operation.
# ------------------------------------------------------------------------------
check_net_cap_bind_host(){
    wrap_debug "Setting the CAP_NET_BIND_SERVICE capability on the host..."
    
      # Check dependencies
    ret=$(need_cmd setcap)
    if [ $? != 0 ]; then 
        wrap_fail "capability_check_host"
        return
    fi
    
    touch cap.txt
    setcap "cap_net_bind_service=+ep" cap.txt
    ret=$?
    if [ $ret != 0 ]; then
        wrap_debug "setcap 'cap_net_bind_service=+ep' returned $ret"
        wrap_fail "capability_check_host"
        return
    fi

    contains=$(getcap cap.txt | grep 'cap_net_bind_service+ep')
    ret=$?
    if [ $? != 0 ] && [ -z "${contains##*cap_net_bind_service+ep*}" ]; then
        wrap_debug "setcap 'cap_net_bind_service=+ep' returned 0, but did not set the capability"
        wrap_fail "capability_check_host"
        return
    fi

    wrap_pass "capability_check_host" "Pass"
}

check_net_cap_bind_container(){
    #Check For Docker
    wrap_debug "Setting the CAP_NET_BIND_SERVICE capability in a container..."
    
    # Check dependencies
    ret=$(need_cmd docker)
    if [ $? != 0 ]; then 
        wrap_fail "capability_check_container" "Fail"
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
        wrap_fail "capability_check_container" "Fail"
        return
    fi
    wrap_pass "capability_check_container" "Pass"
}

# bits of this were adapted from moby check-config.shells
# See https://github.com/moby/moby/blob/master/contrib/check-config.sh

check_cgroup_heirachy()
{   
    EXITCODE=0
    if [ "$(stat -f -c %t /sys/fs/cgroup 2> /dev/null)" = '63677270' ]; then
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


#Todo : Auto-update these as part of build pipeline.
#Represent the highest loading address using by iotedge components(edged + iis + dotnet components)

mmap_aziotedge_aarch64=63856
mmap_aziotedge_x86_64=68136
mmap_aziotedge_armv7=60592

check_mmap_min_addr(){
    lowest_loading_addr=$(sysctl -n vm.mmap_min_addr)
    if [ -z $lowest_loading_addr ]; then
        wrap_fail "check_mmap_min_addr"
        return
    fi

    highest_addr_app=$(eval echo \$mmap_aziotedge_${ARCH})
    if [ -z $highest_addr_app ]; then
        wrap_warning "Loading address of azure iot edge does not exist for architecture $ARCH"
        wrap_fail "check_mmap_min_addr"
        return
    fi
    
    #The address of the application should be above the mmap min addr. See : https://wiki.debian.org/mmap_min_addr
    if [ $highest_addr_app -lt $lowest_loading_addr ]; then
        #Todo : Define Remediation Function
        wrap_warning "The IoT Edge application requires a minimum loading address of $highest_addr_app"
        wrap_fail "check_mmap_min_addr"
    else
        wrap_pass "check_mmap_min_addr"
    fi
}



get_architecture
echo "Architecture:$ARCH"
echo "OS Type:$OSTYPE"

#TODO : Do we need to check in both host and container?
check_net_cap_bind_host
check_net_cap_bind_container

check_cgroup_heirachy
check_mmap_min_addr
perform_cleanup
echo "IoT Edge Compatibility Tool Check Complete"

