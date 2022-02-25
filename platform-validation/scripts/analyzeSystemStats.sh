#! /bin/bash

#This script is used to get Memory and CPU Usage for Provided Binaries and Containers. The script currently stores Peak and Average CPU and Memory Usage
#Units - Memory -MB , CPU - %

#Get Location of script
DIR="$(cd "$(dirname "$0")" && pwd)"
STORAGE_PATH="$(realpath "${STORAGE_PATH:-$DIR}")"
SECONDS_TO_RUN=${SECONDS_TO_RUN:-50}

BINARIES="aziot-edged aziot-identityd aziot-certd aziot-keyd dockerd containerd"
CONTAINERS="edgeHub edgeAgent SimulatedTemperatureSensor"

function usage() {
     echo "$(basename "$0") [options]"
     echo ""
     echo "options"
     echo " -h,  --help                   Print this help and exit."
     echo " -t,  --time                   Time(in seconds) for running memory analysis"
     echo " -b,  --binaries               list of binaries to monitor"
     echo " -c,  --containers             list of containers to monitor"
     echo " -p,  --path                   path where memory file needs to be stored"
     exit 1
}

process_args() {
     save_next_arg=0
     for arg in "$@"; do
          if [ $save_next_arg -eq 1 ]; then
               SECONDS_TO_RUN="$arg"
               save_next_arg=0
          elif [ $save_next_arg -eq 2 ]; then
               BINARIES="$arg"
               save_next_arg=0
          elif [ $save_next_arg -eq 3 ]; then
               CONTAINERS="$arg"
               save_next_arg=0
          elif [ $save_next_arg -eq 4 ]; then
               STORAGE_PATH="$arg"
               save_next_arg=0
          else
               case "$arg" in
               "-h" | "--help") usage ;;
               "-t" | "--time") save_next_arg=1 ;;
               "-b" | "--binaries") save_next_arg=2 ;;
               "-c" | "--containers") save_next_arg=3 ;;
               "-p" | "--path") save_next_arg=4 ;;
               *) usage ;;
               esac
          fi
     done
}

store_stats() {
     binary=$1
     type=$2
     value=$3

     if [[ -f $FILE ]]; then
          stored_peak_stat="$(cat "$FILE" | grep $binary-peak-$type | sed -r "s/^$binary-peak-$type=//g")"
          stored_avg_stat="$(cat "$FILE" | grep $binary-avg-$type | sed -r "s/^$binary-avg-$type=//g")"
     fi

     if [[ -n "$stored_peak_stat" ]]; then
          overwrite=$(echo "$stored_peak_stat" "$value" | awk '{if ($2 > $1) print 1; else print 0}')
          if [[ $overwrite -eq 1 ]]; then
               echo "$(date): Over-writing peak $type usage for $binary New: $value Old: $stored_peak_stat"
               sed -i "s/$binary-peak-$type=[0-9]*.[0-9]*/$binary-peak-$type=$value/" "$FILE"
          fi
     else
          echo "$(date): Writing peak $type usage for $binary, Value: $value"
          echo "$binary-peak-$type=$value" >>"$FILE"
     fi

     if [[ -n "$stored_avg_stat" ]]; then
          avrgd_val=$(echo "$stored_avg_stat" "$value" | awk '{printf "%.2f", ($1+$2)/2}')
          sed -i "s/$binary-avg-$type=[0-9]*.[0-9]*/$binary-avg-$type=$avrgd_val/" "$FILE"
     else
          echo "$(date): Writing avg $type usage for $binary, Value: $value"
          echo "$binary-avg-$type=$value" >>"$FILE"
     fi

}

process_args "$@"
echo "Running Usage Test for $SECONDS_TO_RUN seconds"
FILE="$STORAGE_PATH/usage-$(uname-m).txt"
echo "Storing Data at $FILE"

end_time=$((SECONDS + SECONDS_TO_RUN))
while [[ $SECONDS -lt $end_time ]]; do
     for binary in $BINARIES; do
          memory=$(top -b -d 1 -n 1 | awk '{print $6, $9, $NF}' | grep "$binary$" | awk '{print $1}')
          cpu=$(top -b -d 1 -n 1 | awk '{print $6, $9, $NF}' | grep "$binary$" | awk '{print $2}')

          #We get a Kb Output from top, convert it to Mb for consistency with container data set
          memory=$(echo "$memory" | awk '{printf "%.2f", $1/1024}')
          # echo "Writing memory usage for $binary , Value : $memory"
          # echo "Writing cpu usage for $binary , Value : $cpu"
          store_stats "$binary" "memory" "$memory"
          store_stats "$binary" "cpu" "$cpu"
     done

     for container in $CONTAINERS; do

          #Split XXMiB / YY MiB Output to get XX
          read -a container_memory <<<"$(docker stats --no-stream --format '{{.MemUsage}}' "$container")"
          memory=$(echo "${container_memory[0]}" | sed -r 's/MiB//g')

          #Get XX from XX% from Docker Stats CPU Percentage
          cpu="$(docker stats --no-stream --format '{{.CPUPerc}}' "$container" | sed -r 's/%//g')"
          # echo "Writing memory usage for $container , Value : $memory"
          # echo "Writing cpu usage for $container , Value : $cpu"
          store_stats "$container" "memory" "$memory"
          store_stats "$container" "cpu" "$cpu"
     done

     sleep 5
done
