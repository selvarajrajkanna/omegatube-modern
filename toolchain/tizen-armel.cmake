set(CMAKE_SYSTEM_NAME Linux)
set(CMAKE_SYSTEM_PROCESSOR arm)

set(CMAKE_C_COMPILER arm-linux-gnueabi-gcc)
set(CMAKE_CXX_COMPILER arm-linux-gnueabi-g++)

# Tizen 10 TV userland runs 32-bit AArch32 (ARMv8-A) with the softfp float ABI.
set(TIZEN_ARCH_FLAGS "-march=armv8-a -mfpu=neon-fp-armv8 -mfloat-abi=softfp -mthumb")
set(CMAKE_C_FLAGS_INIT "${TIZEN_ARCH_FLAGS}")
set(CMAKE_CXX_FLAGS_INIT "${TIZEN_ARCH_FLAGS}")

set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
