cmake_minimum_required(VERSION 3.24)
project(volvo_j2534_can_windows VERSION 0.1.0 LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_executable(volvo-j2534-can
    src/main.cpp
    src/j2534/j2534.cpp
    src/can/can_monitor.cpp
)
target_include_directories(volvo-j2534-can PRIVATE src)
if(WIN32)
    target_compile_definitions(volvo-j2534-can PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
endif()
