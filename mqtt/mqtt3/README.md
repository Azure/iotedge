An MQTT v3.1.1 Client implementation in Rust


# Features

- Supports the entire protocol, including all three QoS levels and wills.
- Transparently handles keep-alive pings.
- Transparently reconnects when connection is broken or protocol errors, with back-off.
- Handles subscription and ongoing QoS 1 and QoS 2 publish workflows across reconnections. You don't need to resubscribe or republish messages when the connection is re-established.
- Agnostic to the underlying transport, so it can run over TCP, TLS, WebSockets, etc.
- Standard futures 0.3 and tokio 0.2 interface. The client is just a `futures_core::Stream` of publications received from the server. The underlying transport just needs to implement `tokio::io::AsyncRead` and `tokio::io::AsyncWrite`.


# Documentation

The crate is not published to crates.io yet. Please generate docs locally with `cargo doc`.


# Examples

See the `examples/` directory for examples of a publisher and subscriber, and for how to set a will.

# Fuzz testing

The crate is coming with fuzz tests. 

## Prerequisites

```bash
sudo apt install tmux binutils
cargo install afl --version '^0.6'
```

## Run fuzzer

```bash
# afl requires dumps to be taken as quickly as possible, so configure kernel 
# to just write the coredump instead of anything fancy.
echo 'core' | sudo tee /proc/sys/kernel/core_pattern

# mqtt-fuzz.sh spawns a tmux session with 6 panes. The first pane is the master 
# afl instance, and the remaining five are slave instances.
# Press any key to start the master instance, wait for it to start running, 
# then press any key in the five slave instances to start them too.
build/linux/mqtt-fuzz.sh ../mqtt3-fuzz/

# mqtt-fuzz-rerun.sh uses the output of a previous run of mqtt-fuzz.sh
# or mqtt-fuzz-rerun.sh as the starting corpus.
# Use this if you interrupted a previous run of mqtt-fuzz.sh or mqtt-fuzz-rerun.sh 
# and want to resume from where it left off.
build/linux/mqtt-fuzz-rerun.sh ../mqtt3-fuzz/
```

# License

MIT

