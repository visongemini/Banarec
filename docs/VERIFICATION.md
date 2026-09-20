# BanaStudio 3.0.0 验证记录

日期：2026-09-20

本文区分已执行验证、需在最终源码稳定后重跑的检查，以及受外部条件阻塞的项目。最终打包前必须更新“最终验证”一节，不能把历史结果或 mock 结果写成当前版本通过。

## 已执行验证

| 项目 | 结果 | 证据与边界 |
| --- | --- | --- |
| 原版基线构建 | PASS | 改名前执行 `build.ps1`，ScreenRecorderLib 7.0.1 SHA-256 校验通过并生成 2.2.0 `Banarec.exe`。 |
| BanaStudio 构建 | PASS | 最终源码执行 `build.ps1` 成功；`BanaStudio.exe` 的 FileVersion/ProductVersion 均为 3.0.0.0。 |
| 语音单元测试 | PASS | 当前共享源码执行 `scripts/test-voice.ps1`：31 passed / 0 failed，覆盖设置、Flash WAV 封装、凭据文件边界、会话取消与迟到结果隔离、流式协议解析、词库导入导出和限制。若打包前源码再变更仍需重跑。 |
| Flash 真实识别 | PASS | 生产 C# `FlashAsrClient` 使用 `probe.wav` 返回状态 `20000000`；结果 15 个字符，脱敏散列前缀 `c63104f2`。凭据未写入日志、仓库或分发包。 |
| 原生麦克风边界 | PASS | `scripts/test-voice-native.ps1`：默认麦克风采集 1.3 秒后 `StopAndDrain` 收到 13 包、41,218 字节；随后 `Cancel` 在 181 ms 内返回。未上传、未写音频文件。 |
| 自动粘贴与焦点安全 | PASS | `scripts/test-voice-native.ps1`：实际 WinForms TextBox 自动粘贴一次且剪贴板文本一致；焦点切到 Button 或第二个 TextBox 后旧目标失效并拒绝投递；预先取消的 token 未输入文字。 |
| 语音主页面预览 | PASS | `tests/artifacts/voice-page.png`，由 `scripts/test-voice-preview.ps1` 生成。 |
| 语音悬浮条与词库窗口预览 | PASS | `tests/artifacts/voice-hud.png`、`voice-library.png`、`voice-history.png`，由 `scripts/test-voice-windows.ps1` 生成。最后预览从 `src/Main.xaml` 载入真实 Window.Resources，词库/历史为合成测试数据，未触发持久化操作；主代理已查看词库及历史布局。 |

## 外部阻塞

| 项目 | 状态 | 结论 |
| --- | --- | --- |
| 豆包实时流式 ASR | BLOCKED | `volc.seedasr.sauc.duration` 握手返回 HTTP 400（resource not allowed）；`volc.bigasr.sauc.duration` 返回 HTTP 403（resource not granted）。均在发送音频前失败。当前账户未取得所测流式资源授权，不能宣称流式识别已真实通过。 |

Flash 与实时流式是显式选择的两个后端，不允许在失败后静默切换。

## 最终验证

最终源码与 16:40 生成的分发包验证结果：

- [x] `build.ps1` 无错误，产物名称为 `BanaStudio.exe`，文件版本、产品版本均为 3.0.0.0。
- [x] `scripts/test-voice.ps1` 通过：31 passed / 0 failed。
- [x] `scripts/test-voice-native.ps1` 通过：2 passed / 0 failed。
- [x] `scripts/test-voice-preview.ps1` 与 `scripts/test-voice-windows.ps1` 通过；人工查看 `voice-page.png`、`voice-hud.png`、`voice-library.png`，主动作与常驻字段标签可见。
- [x] 原功能隔离回归 `scripts/test.ps1 -Suite Integration`、`Annotation`、`Preview` 均通过，产物保存在 `tests/artifacts`；覆盖截图、短录屏、停止保存和标注预览。
- [x] 源码审查确认录屏、截图、语音在 Controller 中双向互斥，退出、取消、锁屏路径调用语音清理；原生停止/取消已实测。未模拟真实锁屏事件做端到端验证。
- [x] `scripts/package.ps1` 生成安装包、便携 ZIP 和 `SHA256SUMS.txt`；独立重算两个分发文件哈希一致。
- [x] 便携 ZIP 的 7 个文件逐一 SHA-256 匹配 `release`，只使用 BanaStudio 新文件名；安装脚本保留旧 AppId、旧 mutex/event 和单一自启动值名。
- [x] 当前运行中的已安装版 Banarec（PID 25260、原安装路径）未被终止或覆盖。
- [x] 在读取授权 env 中仅两项 ASR 值进行脱敏扫描后，仓库 tracked/untracked 非忽略文件与 `%LOCALAPPDATA%\BanaStudio\voice-settings.json` 的明文凭据匹配均为 0。

最终分发文件：

- `BanaStudio-Setup-3.0.0.exe`：`b7636f720593b635b153b38a7d5f19d364d39774cf0d77e53c20c1b4d245f37d`
- `BanaStudio-Portable-3.0.0.zip`：`1f44630773a5ee7c75ecf6bb3d5eb54559d31599d9d0c74ea2bde36ba58bae61`

未执行安装包覆盖安装，也未运行会与正在运行的旧 Banarec 争抢全局按键的 Hotkey 桌面测试。实时流式资源仍需账号开通后做真实验证。

## 本机安装验证（后续授权）

用户随后授权本机安装与 GitHub 发布。确认旧版空闲并备份后，退出旧进程，执行 3.0.0 安装程序，退出码为 0，无需重启。系统未检测到旧版安装注册项，因此按新安装写入 `%LOCALAPPDATA%\Programs\BanaStudio`，原程序备份保留。

已安装的 `BanaStudio.exe` 与已验证 `release` 文件 SHA-256 一致，文件/产品版本为 3.0.0.0；开始菜单和桌面快捷方式已创建。实际启动响应正常，进入语音页后显示“极速识别 · 已配置”和“快捷键已就绪 · 兼容模式”。此次未新增麦克风上传测试，也未验证已有安装注册项下的覆盖升级流程。
