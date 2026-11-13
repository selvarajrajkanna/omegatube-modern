# Cross toolchain for Samsung Tizen 10 TVs (armv7l, softfp ABI, GCC 12 for C++20, glibc 2.36).
# Also builds for the x86 (TV 7-9) and x86_64 (TV 10) emulators.
FROM debian:bookworm

RUN apt-get update \
 && apt-get install -y --no-install-recommends g++-arm-linux-gnueabi g++-i686-linux-gnu g++ cmake make file \
 && rm -rf /var/lib/apt/lists/*
