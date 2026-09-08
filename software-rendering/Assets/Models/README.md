# FBX 练习模型

## box.fbx

- 内容：Assimp 官方测试集中的盒体模型，作为首次静态网格导入练习。
- 来源：https://github.com/assimp/assimp/blob/master/test/models/FBX/box.fbx
- 下载日期：2026-09-08。
- 文件大小：17,200 字节；已确认二进制 FBX 文件头。
- 上游许可证随附于 `ASSIMP-LICENSE.txt`。
- 尚未在本项目中执行模型导入或渲染验证。

建议先读取网格、顶点与面数量，启用三角化后检查每个面的索引数量为 3，再接入自己的变换与光栅化流程。模型尺寸、节点变换与绕序应根据实际导入数据检查。
