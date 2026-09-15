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
