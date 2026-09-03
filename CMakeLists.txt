cmake_minimum_required(VERSION 3.24)

project(render_lab LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_executable(render_lab WIN32 src/main.cpp)

target_compile_definitions(render_lab PRIVATE
    UNICODE
    _UNICODE
)

target_link_libraries(render_lab PRIVATE
    d3d11
    dxgi
    d3dcompiler
    user32
)

if(MSVC)
    target_compile_options(render_lab PRIVATE /W4 /permissive-)
else()
    target_compile_options(render_lab PRIVATE -Wall -Wextra -Wpedantic)
endif()
