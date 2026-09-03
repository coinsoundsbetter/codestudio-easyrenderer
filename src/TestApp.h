//
// Created by linjiaxin on 2026/9/3.
//

#ifndef RENDER_LAB_TESTAPP_H
#define RENDER_LAB_TESTAPP_H
#include "Application.h"


class TestApp : public Application {
public:
    TestApp(const std::string& title);

protected:
    bool Load() override;
    void Render() override;
    void Update() override;
};


#endif //RENDER_LAB_TESTAPP_H
