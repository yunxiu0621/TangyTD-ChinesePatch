# payload 目录说明

本仓库出于版权考虑**不包含任何游戏文件**。构建前需自行准备以下 22 个文件，
放置到对应子目录（相对路径须完全一致）：

## payload/patch/ —— 11 个补丁文件

| 文件 | 说明 |
|---|---|
| `TangyTD.exe` | 打过补丁的游戏主程序（含中文字符集节） |
| `game.dll` | 打过补丁的游戏逻辑库（含中文文本节） |
| `credits.txt` | 中文制作人员名单 |
| `assets/fonts/alagard.ttf`<br>`assets/fonts/NameHereCondensed.ttf`<br>`assets/fonts/CelticTime.ttf`<br>`assets/fonts/AKDPixel.ttf`<br>`assets/fonts/PixeloidMono.ttf`<br>`assets/fonts/TinyUnicode.ttf` | 合并了 Fusion Pixel 中文字形的 6 个 UI 字体 |
| `assets/shaders/dx/gpu.hlsl` | 字形图集扩容 512→1024 后的着色器源码 |
| `assets/shaders/dx/compiled/gpu.ps` | 同上的编译后像素着色器 |

## payload/original/ —— 对应 11 个官方原版文件

同构的相对路径，内容为游戏 v1.0.393 官方原版（卸载还原用）。
建议从本地 Steam 安装直接拷贝，或用 Steam「验证文件完整性」取得干净原版。

## 构建校验

`build_patcher.py` 内置以下尺寸断言（v1.0.393）：

- 官方原版：`TangyTD.exe` = 4,357,632 B，`game.dll` = 6,387,200 B
- 补丁版本：`TangyTD.exe` = 19,040,768 B，`game.dll` = 6,415,360 B

尺寸不符会拒绝构建，防止版本错配。
