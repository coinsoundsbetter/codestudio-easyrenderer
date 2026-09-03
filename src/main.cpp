#include <GLFW/glfw3.h>
#include <cstdint>
#include <iostream>
#include "Application.h"
#include "TestApp.h"

int main()
{
    TestApp app("render-lab");
    app.Run();
}
