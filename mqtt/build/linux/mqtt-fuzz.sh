#!/bin/bash

set -euo pipefail

if [ "$(cat /proc/sys/kernel/core_pattern)" != 'core' ]; then
	echo '/proc/sys/kernel/core_pattern not set to core' >&2
	exit 1
fi

tmux attach-session -t mqtt-fuzz && exit 0 || :

DIR=$1
if [ ! -d "$DIR" ]; then
    echo "$DIR does not exist"
fi

cd $DIR

rm -rf in/ out/ target/

cargo afl build --release

mkdir in.tmin/
for f in in/*; do
	cargo afl tmin -i "$f" -o "in.tmin/$(basename "$f")" ./target/release/mqtt3-fuzz
done
rm -rf in/
mv in.tmin/ in/

(
sleep 1

tmux split-window -t 0 -h -p 66 'read -rsp "Press enter to continue..." && cargo afl fuzz -i in -o out -S fuzzer02 -t 1000 target/release/mqtt3-fuzz'
tmux select-pane -t 1 -T 'fuzzer02'

tmux split-window -t 1 -h -p 50 'read -rsp "Press enter to continue..." && cargo afl fuzz -i in -o out -S fuzzer03 -t 1000 target/release/mqtt3-fuzz'
tmux select-pane -t 2 -T 'fuzzer03'

tmux split-window -t 0 -v -p 50 'read -rsp "Press enter to continue..." && cargo afl fuzz -i in -o out -S fuzzer04 -t 1000 target/release/mqtt3-fuzz'
tmux select-pane -t 1 -T 'fuzzer04'

tmux split-window -t 2 -v -p 50 'read -rsp "Press enter to continue..." && cargo afl fuzz -i in -o out -S fuzzer05 -t 1000 target/release/mqtt3-fuzz'
tmux select-pane -t 3 -T 'fuzzer05'

tmux split-window -t 4 -v -p 50 'read -rsp "Press enter to continue..." && cargo afl fuzz -i in -o out -S fuzzer06 -t 1000 target/release/mqtt3-fuzz'
tmux select-pane -t 5 -T 'fuzzer06'

tmux select-pane -t 0 -T 'fuzzer01'
tmux select-pane -t 0
) &

tmux new-session -s mqtt-fuzz 'read -rsp "Press enter to continue..." && cargo afl fuzz -i in -o out -M fuzzer01 -t 1000 target/release/mqtt3-fuzz'
