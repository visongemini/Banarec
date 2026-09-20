# Windows 桌面验证

测试需要已解锁的交互式 Windows 桌面，不适合普通无桌面的 CI。

先通过托盘菜单退出 Banarec，再从仓库根目录运行：

```powershell
.\scripts\test.ps1 -Suite Integration
.\scripts\test.ps1 -Suite Hotkey
.\scripts\test.ps1 -Suite Annotation
.\scripts\test.ps1 -Suite Preview
```

- `Integration`：WPF 渲染、321×181 PNG、剪贴板、截图取消、倒计时取消、重新录制、浮框停止与保存。
- `Hotkey`：从设置按钮录入新组合键，检查默认与自定义截图、录屏切换、设置持久化及 Ctrl+A 正常全选。
- `Annotation`：标注笔迹、荧光笔、撤销及原始分辨率导出。
- `Preview`：导出三个页面的实际 WPF 渲染图。

测试会打开窗口、录制桌面并使用剪贴板。请保存其他工作并收起敏感内容，运行期间不要操作键盘鼠标。脚本恢复运行前的 Banarec 设置；测试尽力恢复剪贴板，但不保证所有外部应用的延迟数据格式都能恢复。

默认输出 `tests/artifacts/`，可用 `BANAREC_TEST_OUTPUT` 指定目录。录像、截图和本机日志不会提交。

区域选择通过注入选区检验输出，不代替真实鼠标拖动的手动验收。长时间录制、锁屏、磁盘耗尽、多 DPI 和独占全屏仍需专门验证。

## 语音纯逻辑测试

```powershell
.\scripts\test-voice.ps1
```

该入口只编译语音数据、设置默认值和安全空闲路径测试，不启动 Banarec，不注册全局快捷键，不访问麦克风、桌面、剪贴板、网络或真实凭据。测试使用系统临时目录并在结束时清理。

语音页可以通过独立 XAML 渲染验证，不创建 `Controller`，因此不会注册 hook 或读写用户设置：

```powershell
.\scripts\test-voice-preview.ps1
```

悬浮条和词库/历史窗口使用虚构数据渲染，不触发任何保存动作：

```powershell
.\scripts\test-voice-windows.ps1
```

语音原生边界测试会依次打开一个隔离的 WinForms 窗口并访问默认麦克风：

```powershell
.\scripts\test-voice-native.ps1
```

该脚本检查普通文本框只自动粘贴一次且剪贴板内容一致；焦点移到按钮或另一输入框后旧目标失效且不会误投；已取消的粘贴不会输入文字；录音 1.3 秒后 `StopAndDrain` 能收到数据，`Cancel` 不会死锁。它不启动或终止 BanaStudio/Banarec，不访问网络，也不把音频写入磁盘。运行时不要同时执行 `Integration`、`Hotkey` 或其他会争夺桌面焦点的测试。

当前会话控制器直接创建网络、麦克风和输入投递对象，因此迟到结果、最终包等待和单次投递还需要可注入的会话协调器后才能做隔离单元测试；这些场景不得用真实桌面或真实麦克风代替。

## 手动服务探针

固定 WAV 可以直接验证生产 Flash 客户端，不读取麦克风，也不会保存或打印凭据：

```powershell
.\scripts\probe-voice-flash.ps1 -WavPath C:\path\to\probe.wav -EnvFile C:\path\to\keys.env
```

流式授权排查使用 `tests/volc_stream_probe.py`。它按 200 ms 发送 16 kHz 单声道 PCM，仅输出资源 ID、脱敏握手状态、服务码、错误消息和转写。该探针依赖 Python `websockets`，不得加入默认测试或保存真实凭据。
