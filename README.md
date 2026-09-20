<p align="center">
  <img src="assets/BanaStudio.png" width="112" alt="蕉仔拿着摄像机的 BanaStudio 图标" />
</p>

<h1 align="center">BanaStudio</h1>

<p align="center">录屏 · 截图 · 语音识别</p>

<p align="center">
  <a href="https://github.com/visongemini/Banarec/releases/tag/v3.0.0">下载 v3.0.0</a> ·
  <a href="#快捷键">快捷键</a> ·
  <a href="#从源码构建">从源码构建</a>
</p>

## 界面

<p align="center">
  <img src="docs/images/record.png" width="420" alt="BanaStudio 录屏界面" />
  <img src="docs/images/screenshot.png" width="420" alt="BanaStudio 截图界面" />
</p>

半透明磨砂界面、圆角控件、独立的录屏与截图入口。图标是拿着摄像机的蕉仔 Banny。

## 功能

### 录屏

- 选择显示器，全屏录制或拖动框选区域。
- 按原始像素录制，保存为高画质 H.264 MP4；可选 30 / 60 FPS。
- 系统声音与麦克风独立开关，两者都开时混合为一条音轨。
- 3 秒倒计时，录制时显示红色区域边框。
- 可拖动的置顶浮框，显示计时、声音状态及停止按钮。
- 在支持的 Windows 环境中，边框和浮框不出现在录像中。
- 提供 CPU 兼容模式；正常退出时先停止并保存录像。

### 截图

- **Ctrl + Alt + A** 启动框选截图，支持多个显示器。
- 框选后进入标注窗口：画笔、荧光笔、橡皮擦、7 种颜色和笔迹粗细。
- 支持撤销 / 重做；点击“完成并复制”后保存原始分辨率 PNG，同时复制到剪贴板。
- Esc 或鼠标右键取消。
- 保留选区的实际像素尺寸，包括奇数宽高。

### 语音识别

- 默认使用豆包 Flash ASR，说完后一次返回识别结果；也可显式选择需单独开通授权的实时流式后端。当前开发账户尚未取得所测流式资源授权，流式模式未完成真实验收。
- 语音快捷键支持“按一下开始、再按一下结束”和“按住讲话、松开结束”两种操作方式。
- 最终文字会保留到剪贴板；只有原输入框仍可确认且焦点未变化时才自动粘贴一次。目标失效、不可编辑或权限受限时只保留剪贴板，不会误投到其他控件，也不会自动发送消息。
- 支持自定义热词、本地转写历史、词库 JSON / CSV / TXT 导入、JSON / CSV 导出，以及把历史中的纠错加入热词词库；默认不保存原始音频。
- 只有启用语音识别时，麦克风音频和已启用的热词才会发送给豆包 ASR。

### 保存与常驻

- 文件自动保存在 Windows **下载**文件夹，支持重定向后的下载位置。
- 按日期、时间及短随机后缀命名，避免覆盖。
- 关闭主窗口后驻留托盘；右键托盘可完全退出。
- 可设置登录自动启动，启动时只驻留托盘。
- 截图、录屏与语音快捷键都可在应用中自定义，并自动保存。
- 录屏和截图在本机处理，不上传录制内容。

### 截图标注

![截图标注窗口](docs/images/annotation.png)

## 安装

v3.0.0 发布后可下载 [安装包](https://github.com/visongemini/Banarec/releases/download/v3.0.0/BanaStudio-Setup-3.0.0.exe) 或 [便携版 ZIP](https://github.com/visongemini/Banarec/releases/download/v3.0.0/BanaStudio-Portable-3.0.0.zip)。

1. 下载并运行 `BanaStudio-Setup-3.0.0.exe`。
2. 运行安装程序，可选择创建桌面快捷方式和登录自动启动。
3. 从桌面或开始菜单打开 **BanaStudio**。

### 系统要求

- Windows 10 2004 或更新版本 / Windows 11，x64。
- .NET Framework 4.8。
- Microsoft Visual C++ 2015–2022 x64 运行库。
- 系统可用的 Media Foundation；Windows N / KN 版本需安装对应媒体功能包。

安装包不捆绑上述系统运行库。未进行代码签名的版本可能显示“未知发布者”。

## 快捷键

| 操作 | 快捷键 |
| --- | --- |
| 框选截图 | **Ctrl + Alt + A** |
| 开始 / 停止录屏 | **Ctrl + Shift + F9** |
| 开始 / 停止语音识别 | **Ctrl + Alt + Space** |
| 取消区域选择 | **Esc** 或右键 |

表中是默认值。截图和录屏快捷键在**偏好设置**中修改，语音快捷键在**语音识别**页修改；新快捷键需包含 Ctrl、Alt 或 Shift。

当其他程序已注册所选组合键时，BanaStudio 会启用兼容方式响应。Ctrl + A 等未设置的组合键保持原有行为。

该兼容机制只判断已设置的三个组合键，不收集、保存或传输键入内容。

## 使用说明

- **系统声音**使用 Windows 默认输出设备；**麦克风**使用默认输入设备。
- 没有麦克风声音时，检查默认输入设备及 Windows 桌面应用麦克风权限。
- 硬件录制失败时，可在偏好设置中开启**兼容模式**后重试。
- 正常停止后，等待保存完成再关机。强制结束进程可能导致 MP4 不完整。
- H.264 录屏宽高需对齐偶数，奇数选区会向内减少 1 像素；截图不受此限制。
- 录屏一次选择一块显示器中的区域；截图可以跨显示器。
- 正在录屏时，需要先结束当前录制再截图。
- 实际帧率和清晰度受屏幕、编码器和电脑性能影响；不会通过放大画面制造更高分辨率。

### 文件位置

| 内容 | 位置 |
| --- | --- |
| 安装目录 | 新安装：`%LOCALAPPDATA%\Programs\BanaStudio`；从旧版升级：沿用原目录 |
| 设置 | `%LOCALAPPDATA%\BanaStudio\settings.ini` |
| 错误日志 | `%LOCALAPPDATA%\BanaStudio\errors.log` |
| 录像与截图 | Windows 下载文件夹 |

首次启动会把原 `%LOCALAPPDATA%\Banarec\settings.ini` 复制到新目录；原文件保留，不会丢失旧设置。安装程序保留原 AppId，并把原有的单一登录启动项更新为 `BanaStudio.exe`，不会创建两份自启动。

通过 Windows **设置 → 应用 → BanaStudio**卸载。卸载不会删除已保存的截图和录像。

## 从源码构建

项目使用 **C#、WPF、WinForms 和 ScreenRecorderLib 7.0.1**。构建脚本使用 Windows .NET Framework 自带的 x64 C# 编译器。

在 Windows PowerShell 中，从仓库根目录运行：

```powershell
.\build.ps1
```

脚本会下载固定版本的 ScreenRecorderLib NuGet 包并校验 SHA-256，然后生成 `release\BanaStudio.exe`。首次构建需要联网，后续可使用缓存。无需把凭据写入项目。

### 构建安装包

安装 [Inno Setup 6](https://jrsoftware.org/isdl.php) 后运行：

```powershell
.\scripts\package.ps1 -CompilerPath 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

输出为 `dist\BanaStudio-Setup-3.0.0.exe`、`dist\BanaStudio-Portable-3.0.0.zip` 和 `dist\SHA256SUMS.txt`。安装包以当前用户权限安装，不需要管理员服务。

### 验证

```powershell
# 需要已解锁的 Windows 桌面；会临时打开测试窗口并生成测试录像
.\scripts\test.ps1 -Suite Integration

# 额外检查全局截图快捷键和 Ctrl+A
.\scripts\test.ps1 -Suite Hotkey
```

测试会短暂使用屏幕和剪贴板，并尝试恢复原有剪贴板及设置。测试输出保存在 `tests\artifacts`，不会提交到仓库。请在没有敏感内容的桌面运行。详见 [测试说明](tests/README.md)。

## 验证状态

2.2 版原有功能曾在开发机验证：

- 录屏、截图和设置界面渲染。
- PNG 选区尺寸、剪贴板复制、截图取消，以及笔迹颜色、荧光笔、橡皮擦、撤销 / 重做和原始尺寸导出。
- 倒计时取消、重新录制、浮框停止及 MP4 保存。
- 新版录制文件通过 FFmpeg 完整解码检查。
- 默认与自定义截图快捷键、自定义录屏快捷键、快捷键持久化及 Ctrl + A 隔离。
- Windows 11 半透明磨砂背景、圆角主窗口、截图标注窗口和录制浮框。
- 安装、桌面 / 开始菜单快捷方式及托盘启动模式。

3.0 版已在开发机验证 Flash 真实识别、1.3 秒麦克风采集排空、普通文本框单次自动粘贴、切换焦点后拒绝误投，以及 31 项语音逻辑测试。原有截图、短录屏、停止保存和标注的隔离回归均通过，详见 [验证记录](docs/VERIFICATION.md)。实时流式后端因所测账户未取得对应资源授权而未完成真实识别；覆盖安装和全局 Hotkey 桌面测试未执行。长时间连续录制、4K / 60 FPS、特殊独占全屏、不同 DPI 的多屏组合，以及其他电脑的兼容性仍未完整验证。受保护的视频和安全桌面可能无法录制。

## 目录

```text
src/             应用、界面、录制和截图逻辑
assets/          蕉仔应用图标
docs/            界面预览、版本说明
scripts/         依赖恢复、安装包和测试脚本
tests/           Windows 桌面集成测试源码
third_party/     第三方许可和安装程序语言文件
build.ps1        应用构建入口
installer.iss    Inno Setup 安装脚本
```

## 第三方与素材

- [ScreenRecorderLib](https://github.com/sskodje/ScreenRecorderLib)：MIT，完整许可见 [third_party/ScreenRecorderLib.LICENSE](third_party/ScreenRecorderLib.LICENSE)。
- 安装程序使用 Inno Setup；构建者应遵循其使用条款。
- 简体中文安装语言文件来自 [Inno Setup Chinese Simplified Translation](https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation)。
- 蕉仔图标基于项目提供的角色设定图，通过 imagegen 生成。详见 [素材说明](assets/README.md)。

本项目暂未指定代码的开源许可证。仓库公开不代表对蕉仔角色、名称或品牌素材授予额外使用许可。
