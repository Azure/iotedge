#! /bin/bash

#This script is used to get Memory and CPU Usage for Provided Binaries and Containers. The script currently stores Peak and Average CPU and Memory Usage
#Units - Memory -MB , CPU - % , Storage Size - MB

#Get Location of script
DIR="$(cd "$(dirname "$0")" && pwd)"
STORAGE_PATH="$(realpath "${STORAGE_PATH:-$DIR}")"
SECONDS_TO_RUN=${SECONDS_TO_RUN:-50}
INTERVAL=${INTERVAL:-0}

BINARIES="aziot-edged aziot-identityd aziot-certd aziot-keyd"
BINARYLOCATIONS="/usr/bin /usr/libexec"
CONTAINERS="edgeHub edgeAgent"

function usage() {
     echo "$(basename "$0") [options]"
     echo ""
     echo "options"
     echo " -h,  --help                   Print this help and exit."
     echo " -t,  --time                   Time(in seconds) for running memory analysis, Default is 50 seconds"
     echo " -b,  --binaries               list of binary names to monitor, defaults to iot edge binaries"
     echo " -c,  --containers             list of container names to monitor, defaults to edge runtime containers"
     echo " -p,  --path                   path where memory file needs to be stored, defaults to script execution path"
     echo " -l,  --binaryLocations        location where binaries are present, defaults to /usr/bin /usr/libexe"
     echo " -a    --analyze               Analyzes the Memory File"
     echo " -i,  --interval              interval(in secs) to use for polling memory and cpu stats"
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
          elif [ $save_next_arg -eq 5 ]; then
               MEMORY_FILE_PATH="$arg"
               save_next_arg=0
          elif [ $save_next_arg -eq 6 ]; then
               INTERVAL="$arg"
               save_next_arg=0
          else
               case "$arg" in
               "-h" | "--help") usage ;;
               "-t" | "--time") save_next_arg=1 ;;
               "-b" | "--binaries") save_next_arg=2 ;;
               "-c" | "--containers") save_next_arg=3 ;;
               "-p" | "--path") save_next_arg=4 ;;
               "-a" | "--analyze") save_next_arg=5 ;;
               "-i" | "--interval") save_next_arg=6 ;;
               *) usage ;;
               esac
          fi
     done
}

store_stats() {
     binary=$1
     type=$2
     value=$3

     #Check if value passed is not number
     re='^[0-9]+$'
     if [[ $value =~ $re ]]; then
          return
     fi

     if [[ -f $FILE ]]; then
          stored_peak_stat="$(grep "$binary"-peak-"$type" <"$FILE" | sed -r "s/^$binary-peak-$type=//g")"
          stored_avg_stat="$(grep "$binary"-avg-"$type" <"$FILE" | sed -r "s/^$binary-avg-$type=//g")"
     fi

     if [[ -n $value ]]; then
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
     fi

}

perform_analysis() {
     IOTEDGE_BINARIES_SIZE=0.0
     IOTEDGE_BINARIES_MEMORY=0.0
     IOTEDGE_CONTAINERS_SIZE=0.0
     IOTEDGE_CONTAINERS_MEMORY=0.0

     for binary in $BINARIES; do
          if [[ $binary =~ aziot-* ]]; then
               binary_size="$(grep "$binary"-size <"$1" | sed -r "s/$binary-size=//g")"
               memory_usage="$(grep "$binary"-avg-memory <"$1" | sed -r "s/$binary-avg-memory=//g")"
               IOTEDGE_BINARIES_SIZE=$(echo "$IOTEDGE_BINARIES_SIZE" "$binary_size" | awk '{print $1 + $2}')
               IOTEDGE_BINARIES_MEMORY=$(echo "$IOTEDGE_BINARIES_MEMORY" "$memory_usage" | awk '{print $1 + $2}')
          fi
     done

     if [[ -n "$CONTAINERS" ]]; then
          for container in $CONTAINERS; do
               if [[ $container =~ edgeAgent ]] || [[ $container =~ edgeHub ]]; then
                    memory_usage="$(grep "$container"-avg-memory <"$1" | sed -r "s/$container-avg-memory=//g")"
                    unique_container_size="$(grep "$container"-unique-size <"$1" | sed -r "s/$container-unique-size=//g")"
                    shared_container_size="$(grep "$container"-shared-size <"$1" | sed -r "s/$container-shared-size=//g")"
                    IOTEDGE_CONTAINERS_MEMORY=$(echo "$IOTEDGE_CONTAINERS_MEMORY" "$memory_usage" | awk '{print $1 + $2}')

                    # Edge Agent and Edge Hub share a common layer and hence we only need to add the shared layer once
                    # Todo : Is there a better way to do this? In future, they may not share a layer and we need to be careful about that.
                    if [[ $container =~ edgeAgent ]]; then
                         IOTEDGE_CONTAINERS_SIZE=$(echo "$IOTEDGE_CONTAINERS_SIZE" "$unique_container_size" "$shared_container_size" | awk '{print $1 + $2 + $3}')
                    else
                         IOTEDGE_CONTAINERS_SIZE=$(echo "$IOTEDGE_CONTAINERS_SIZE" "$unique_container_size" | awk '{print $1 + $2 }')
                    fi

               fi
          done
     fi

     echo "$(uname -m)_iotedge_binaries_size=$IOTEDGE_BINARIES_SIZE"
     echo "$(uname -m)_iotedge_binaries_avg_memory=$IOTEDGE_BINARIES_MEMORY"
     echo "$(uname -m)_iotedge_container_size=$IOTEDGE_CONTAINERS_SIZE"
     echo "$(uname -m)_iotedge_container_memory=$IOTEDGE_CONTAINERS_MEMORY"
}

process_args "$@"
if [[ -n $MEMORY_FILE_PATH ]]; then
     if [[ -f $MEMORY_FILE_PATH ]]; then
          perform_analysis "$MEMORY_FILE_PATH"
          exit 0
     else
          echo "File $MEMORY_FILE_PATH does not exist"
          exit 1
     fi
fi

echo "Running Usage Test for $SECONDS_TO_RUN seconds with INTERVAL $INTERVAL seconds"
FILE="$STORAGE_PATH/usage-$(uname -m).txt"
echo "Storing Data at $FILE"
echo "Binaries : $BINARIES"
echo "Binary Locations : $BINARYLOCATIONS"
echo "Containers: $CONTAINERS"
echo "Runtime_Seconds=$SECONDS_TO_RUN"
echo "OS=$(grep PRETTY_NAME <"/etc/os-release" | sed -r 's/^PRETTY_NAME=//g')"
echo "ARCH=$(uname -m)"
echo "Pruning all unused images"
sudo docker system prune --all --force

end_time=$((SECONDS + SECONDS_TO_RUN))
while [[ $SECONDS -lt $end_time ]]; do
     for binary in $BINARIES; do
          for location in $BINARYLOCATIONS; do
               file_name=$(find "$location" -name "$binary")
               if [[ -n $file_name ]]; then
                    break
               fi
          done

          if [[ -n $file_name ]]; then
               # We only need to store Size of binary once
               if [[ -f $FILE ]]; then
                    stored_binary_size="$(grep "$binary"-size <"$FILE" | sed -r "s/^$binary-size=//g")"
               fi
               if [[ -z "$stored_binary_size" ]]; then
                    file_size=$(stat -c%s "$(readlink -f "$file_name")")
                    #We get a byte Output from stat, convert it to Mb for consistency with container data set
                    file_size=$(echo "$file_size" | awk '{printf "%.2f", $1/1024576}')
                    echo "Writing Binary Size for $binary : $file_size"
                    echo "$binary-size=$file_size" >>"$FILE"
               fi

               #Top doesn't seem to get Memory CPU in 1ES Agents, Use PS command to get RSS Memory and CPU%
               memory="$(ps up "$(pgrep -f -n "$binary")" 2>/dev/null | awk '{print $6}' | sed -r 's/([^0-9)+(.[0-9])?//g' | tr -d '\n')"
               cpu="$(ps -p "$(pgrep -f -n "$binary")" -o %cpu 2>/dev/null | sed -r 's/([^0-9)+(.[0-9])?//g' | tr -d '\n')"

               echo "$binary CPU is $cpu Memory is $memory"

               #We get a Kb Output from top, convert it to Mb for consistency with container data set
               memory=$(echo "$memory" | awk '{printf "%.2f", $1/1024}')
               # echo "Writing memory usage for $binary , Value : $memory"
               # echo "Writing cpu usage for $binary , Value : $cpu"
               store_stats "$binary" "memory" "$memory"
               store_stats "$binary" "cpu" "$cpu"
          fi
     done

     for container in $CONTAINERS; do

          imageId=$(docker inspect --format '{{.Image}}' "$container" 2>/dev/null | sed -r 's/sha256://g') #Split XXMiB / YY MiB Output to get XX
          if [[ -n $imageId ]]; then
               if [[ -f $FILE ]]; then
                    stored_unique_container_size="$(grep "$container"-unique-size <"$FILE" | sed -r "s/^$container-unique-size=//g")"
                    stored_shared_container_size="$(grep "$container"-shared-size <"$FILE" | sed -r "s/^$container-shared-size=//g")"
               fi

               if [[ -z "$stored_unique_container_size" ]]; then
                    imageId=${imageId:0:12}

                    # Because Layers can be overlapping, record the unique and shared layer sizes for each of the containers.

                    # r option here doesnt yield the correct result, need to investigate why
                    read -a container_size <<<"$(docker system df -v --format='{{range .Images}}{{printf "%s %s %s" .ID .SharedSize .UniqueSize}}{{println}}{{end}}' | grep "$imageId")"

                    stored_shared_container_size=$(echo "${container_size[1]}" | sed -r 's/([^0-9)+(.[0-9])?//g')
                    stored_unique_container_size=$(echo "${container_size[2]}" | sed -r 's/([^0-9)+(.[0-9])?//g')
                    # Output of the form <sha> <size>MB
                    echo "Writing Container Size for $container : Shared $stored_shared_container_size Unique : $stored_unique_container_size"
                    echo "$container-unique-size=$stored_unique_container_size" >>"$FILE"
                    echo "$container-shared-size=$stored_shared_container_size" >>"$FILE"
               fi

               # r option here doesnt yield the correct result, need to investigate why
               read -a container_memory <<<"$(docker stats --no-stream --format '{{.MemUsage}}' "$container")"
               memory=$(echo "${container_memory[0]}" | sed -r 's/MiB//g')

               #Get XX from XX% from Docker Stats CPU Percentage
               cpu="$(docker stats --no-stream --format '{{.CPUPerc}}' "$container" | sed -r 's/%//g')"
               # echo "Writing memory usage for $container , Value : $memory"
               # echo "Writing cpu usage for $container , Value : $cpu"
               store_stats "$container" "memory" "$memory"
               store_stats "$container" "cpu" "$cpu"
          fi

     done

     sleep "$INTERVAL"

done
