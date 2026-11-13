# 智慧窗 / SmartWindow

**Ver 1.0.1**

智慧窗是一款面向 Windows 11 的顶部通知与音乐控制浮窗工具。它将系统通知和当前媒体信息显示在屏幕顶部中央，并提供类似灵动岛的展开、收拢与音乐控制体验。

## 功能

- 将 Windows 通知从右下角横幅转向顶部胶囊浮窗。
- 显示当前媒体、专辑封面、歌曲信息与动态波形。
- 提供上一首、暂停/继续、下一首控制。
- 支持透明智慧窗、质感增强、光粒子和粒子擦除动画。
- 支持控制台透明度与 80%–120% 窗口缩放。
- 支持开机自启动。
- 支持中文、English、日本語、한국어、Русский、Français。
- 支持 `Ctrl+M` 音乐模式与 `Ctrl+M+L` 音乐窗常驻快捷键。

## 直接使用

运行根目录的 `SmartWindow.exe`。

`SmartWindow.exe` 已内嵌 Logo、汉仪文黑 85W 字体和媒体辅助脚本，可以单独分发。

完整压缩包：

- `SmartWindow-Ver1.0.1.zip`

## 开机自启动

打开智慧窗控制台，在控制中心底部开启“开机自启动”。程序会写入当前用户的 Windows Run 启动项，并在登录后以后台方式启动。

关闭该选项会删除对应启动项，不需要管理员权限。

## 从源码构建

进入 `源码` 目录并运行：

```powershell
.\build.ps1
```

构建需要 Windows 自带的 .NET Framework 4.x C# 编译器。

## 系统要求

- Windows 11
- 无需管理员权限
- 无需额外安装 .NET 运行库

UAC、安全桌面、部分驱动工具自绘提示和全屏独占程序中的提示可能无法被普通 Windows 通知接口接管。

## 隐私

通知和媒体信息均从 Windows 本地接口读取。智慧窗不要求登录账号，也不会上传通知、音乐或播放历史。

## 作者

Sendyun Technology Unit  
作者：Mr.Dove  
反馈邮箱：Mr.Dove@aliyun.com

GitHub：https://github.com/Sendyun/SmartWindow

