# Tangy TD 简体中文补丁工具

> **本项目由 AI 编写（GLM-5.3，ZCode 辅助开发），经人工审阅后发布。**

Steam 版 Tangy TD（v1.0.393）简体中文补丁的单文件安装/卸载工具。
补丁数据（11 个补丁文件 + 11 个官方原版文件）以 deflate 压缩内嵌在 exe 中，
无需安装流程，游戏目录自动探测（本程序所在目录 → 注册表 Steam 库 → 盘符简单搜索）。

## 获取补丁数据

出于版权考虑，本仓库**不包含任何游戏文件**（`payload/`、`build/`、`dist/` 均不入库，
见 `.gitignore` 与 `payload/README.md`）。克隆后需自行放置 22 个文件
（11 个补丁文件 + 11 个官方原版）才能构建。

## 构建

依赖：Windows 自带 .NET Framework 4.x csc + Python 3（仅构建时需要，成品零依赖）。

```
python build_patcher.py
```

产物（文件名带版本号）：
- `dist/TangyTD_简体中文补丁_v1.0.393.exe` —— 补丁工具（GUI：安装/卸载两个按钮；
  另支持命令行 `/<install|uninstall> [/dir=路径]`，成功 rc=0，失败 rc=2）
- `dist/TangyTD_简体中文补丁_v1.0.393.zip` —— 分发包（exe + 使用说明 + 字体 OFL 授权）

## 目录结构

```
src/Patcher.cs        界面 + 逻辑（C# 5，兼容系统自带 csc）
payload/patch/        11 个补丁文件（由游戏目录 chinese_patch/ 构建链产出）
payload/original/     对应 11 个官方原版文件（卸载还原用）
licenses/             Fusion Pixel 字体 SIL OFL 1.1 授权文本
build/                构建中间产物（Manifest.cs 由脚本生成，勿手改；*.bin 压缩资源）
```

## 安装/卸载行为

- 安装：校验游戏未运行、TangyTD.exe/game.dll 尺寸为原版或已打补丁（锁版本 1.0.393），
  覆盖 11 个补丁文件，config.ini 仅改 `language=1` 一行（保留玩家其它设置）
- 卸载：同样校验后还原 11 个官方原版文件，`language=0`
- 存档/成就不受影响；Steam「验证文件完整性」会还原为原版（等同卸载）

## 升级补丁数据

1. 用 `chinese_patch/`（游戏目录内）的构建链重新生成补丁文件
2. 更新 `payload/patch`、`payload/original`，并同步 `build_patcher.py`
   中的 `VERSION` 与 `EXPECT_ORIG/EXPECT_PATCHED` 尺寸表
3. 重新 `python build_patcher.py`

## 许可证

- 本项目代码与文档：[MIT License](LICENSE)（Copyright (c) 2026 yunxiu0621）
- 内嵌中文像素字形：[Fusion Pixel Font](https://github.com/TakWolf/fusion-pixel-font)，
  采用 [SIL Open Font License 1.1](licenses/) 授权（`licenses/` 目录附全文）
- Tangy TD 及其游戏素材版权归游戏开发者所有，本工具与仓库不分发任何游戏文件
