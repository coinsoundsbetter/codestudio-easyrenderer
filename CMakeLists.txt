cmake_minimum_required(VERSION 3.24)

project(render_lab LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

find_package(glfw3 CONFIG REQUIRED)
find_package(glad CONFIG REQUIRED)
find_package(glm CONFIG REQUIRED)

add_executable(render_lab src/main.cpp)
target_link_libraries(render_lab PRIVATE glfw glad::glad glm::glm)

if(MSVC)
  target_compile_options(render_lab PRIVATE /W4 /permissive-)
else()
  target_compile_options(render_lab PRIVATE -Wall -Wextra -Wpedantic)
endif()
