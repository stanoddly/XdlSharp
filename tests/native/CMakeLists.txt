cmake_minimum_required(VERSION 3.16)
project(xdlsharp_native LANGUAGES C)

set(XDL_INCLUDE_DIRECTORY "" CACHE PATH "The include directory of an XDL install")
if(NOT EXISTS "${XDL_INCLUDE_DIRECTORY}/SDL3/SDL.h")
    message(FATAL_ERROR "XDL_INCLUDE_DIRECTORY must name the include directory of an XDL install")
endif()

# The C side of the checks, from the same headers as the bindings. It uses no SDL function, so it links nothing.
add_library(xdlsharp_native SHARED xdlsharp_layout.c xdlsharp_macros.c)
target_include_directories(xdlsharp_native PRIVATE "${XDL_INCLUDE_DIRECTORY}")
set_target_properties(xdlsharp_native PROPERTIES C_STANDARD 11 C_VISIBILITY_PRESET hidden)
