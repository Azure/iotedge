#!/bin/bash

###############################################################################
# This Script uses openssl to generate a self signed certificate
###############################################################################

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_NAME=$(basename $0)
SSL_CERTIFICATE_COMMON_NAME=${SSL_CERTIFICATE_COMMON_NAME:="$HOSTNAME"}

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " -p, --ssl_cert_path  SSL Certificate Path"
    echo " -f, --ssl_cert_file  SSL Certificate File"
    echo " -cn, --ssl_cert_cn   SSL Certificate Common Name. Optional, if none provided, $HOSTNAME will be used"
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in $@
    do
        if [ $save_next_arg -eq 1 ]; then
            SSL_CERTIFICATE_PATH="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            SSL_CERTIFICATE_NAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            SSL_CERTIFICATE_COMMON_NAME="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-p" | "--ssl_cert_path" ) save_next_arg=1;;
                "-f" | "--ssl_cert_file" ) save_next_arg=2;;
                "-cn" | "--ssl_cert_cn" ) save_next_arg=3;;
                * ) usage;;
            esac
        fi
    done

    if [[ ! -d $SSL_CERTIFICATE_PATH ]]; then
        echo "Invalid SSL Certificate Path Provided"
        print_help_and_exit
    fi

    if [[ -z $SSL_CERTIFICATE_NAME ]]; then
        echo "Invalid SSL Certificate Name Provided"
        print_help_and_exit
    fi

    if [[ -z $SSL_CERTIFICATE_COMMON_NAME ]]; then
        echo "Invalid SSL Certificate Path Provided"
        print_help_and_exit
    fi
}

###############################################################################
# Function to generate a cert
###############################################################################
generate_cert()
{
    command="openssl req -nodes -new -x509 -keyout /etc/ssl/private/mqtt-server.key -out $SSL_CERTIFICATE_PATH/mqtt-server.crt -subj /CN=$SSL_CERTIFICATE_COMMON_NAME"

    $command

    if [ $? -ne 0 ]; then
        echo "Failed to generate certificate."
        exit 1
    fi

    command="openssl pkcs12 -export -out $SSL_CERTIFICATE_PATH/$SSL_CERTIFICATE_NAME -inkey /etc/ssl/private/mqtt-server.key -in $SSL_CERTIFICATE_PATH/mqtt-server.crt -passout pass:"

    $command

    if [ $? -ne 0 ]; then
        echo "Failed to generate certificate."
        exit 1
    fi

    echo "Certificate generated successfully!"
}

###############################################################################
# Main Script Execution
###############################################################################
process_args $@

generate_cert

[ $? -eq 0 ] || exit $?
