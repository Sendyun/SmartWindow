# 智慧窗 Ver 1.3.0

Ver 1.3.0 主要更新：

- 完成 Windows 10 1809（Build 17763）及以上版本适配。
- Windows 10 与 Windows 11 统一支持通知、音乐、计时器、磨砂、粒子和停靠功能。
- MSIX 最低系统版本下调至 Windows 10 Build 17763。
- 新增系统版本兼容检测和不受支持系统提示。
- 媒体监听在系统版本或系统能力不足时安静停用，避免后台进程反复重启。
- 修复倒计时关闭后铃声可能继续播放的问题。
- 程序、安装器、控制台和商店程序包统一升级到 Ver 1.3.0。

## 下载

- 安装程序：`SmartWindow-Setup-x64.exe`
- 便携版：`SmartWindow.exe`
- Microsoft Store MSIX：`SmartWindow_1.3.0.0_x64.msix`
- Partner Center 上传包：`SmartWindow_1.3.0.0_x64.msixupload`
- 完整源码：`SmartWindow-1.3.0-Source.zip`
- 默认可替换铃声：`timer-alert.wav`
- 铃声替换说明：`Timer-Alert-Notes.txt`

安装器支持静默安装：

```powershell
.\SmartWindow-Setup-x64.exe /S
```

校验所有发布文件的 SHA-256，请参考 `SHA256SUMS.txt`。

## 系统要求

- Windows 10 1809（Build 17763）或更高版本
- Windows 11
- x64
- 无需管理员权限

作者：Mr.Dove（Sendyun Technology Unit）  
反馈邮箱：Mr.Dove@aliyun.com  
官网：https://cnsendyun.com
