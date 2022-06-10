#!/bin/bash

set -euo pipefail

tmux attach-session -t mqtt-fuzz && exit 0 || :

DIR=$1
if [ ! -d "$DIR" ]; then
    echo "$DIR does not exist"
fi

cd $DIR

rm -rf in/ in.cmin/ in.tmin/

mkdir in.cmin/
i=0
for f in out/fuzzer*/queue/*; do
    cp "$f" "in.cmin/$i"
    i=$(( i + 1 ))
done

mkdir in.tmin/
cargo afl cmin -i in.cmin/ -o in.tmin/ target/release/mqtt3-fuzz
rm -rf in.cmin/

mkdir in.cmin/
for f in in.tmin/*; do
    cargo afl tmin -i "$f" -o "in.cmin/$(basename $f)" target/release/mqtt3-fuzz
done
rm -rf in.tmin/

mkdir in/
cargo afl cmin -i in.cmin/ -o in/ target/release/mqtt3-fuzz
rm -rf in.cmin/

rm -rf out/

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
