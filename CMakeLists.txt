cmake_minimum_required(VERSION 3.24)

project(render_lab LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# GLFW 只负责窗口和输入；D3D11 继续负责图形 API 与渲染。
include(FetchContent)
set(GLFW_BUILD_DOCS OFF CACHE BOOL "" FORCE)
set(GLFW_BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(GLFW_BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
FetchContent_Declare(
    glfw
    GIT_REPOSITORY https://github.com/glfw/glfw.git
    GIT_TAG 3.4
    GIT_SHALLOW TRUE
)
FetchContent_MakeAvailable(glfw)

add_executable(render_lab
    src/main.cpp
        src/Application.cpp
        src/Application.h
        src/TestApp.cpp
        src/TestApp.h
)

target_compile_definitions(render_lab PRIVATE
    RENDER_LAB_SOURCE_DIR=L\"${CMAKE_CURRENT_SOURCE_DIR}\"
)

target_compile_definitions(render_lab PRIVATE
    UNICODE
    _UNICODE
)

target_link_libraries(render_lab PRIVATE
    glfw
    d3d11
    dxgi
    d3dcompiler
    user32
)

if(MSVC)
    # 源码中的中文注释以 UTF-8 保存；显式指定编码，避免系统代码页解释错误。
    target_compile_options(render_lab PRIVATE /W4 /permissive- /utf-8)
else()
    target_compile_options(render_lab PRIVATE -Wall -Wextra -Wpedantic)
endif()
