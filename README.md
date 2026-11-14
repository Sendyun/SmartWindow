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

Microsoft Store 安装程序：

- [`releases/1.0.1/SmartWindow-Setup-x64.exe`](releases/1.0.1/SmartWindow-Setup-x64.exe)
- 静默安装：`SmartWindow-Setup-x64.exe /S`
- 静默卸载：`Uninstall SmartWindow.exe /uninstall /S`

商店提交使用的固定版本下载地址：

```text
https://raw.githubusercontent.com/Sendyun/SmartWindow/main/releases/1.0.1/SmartWindow-Setup-x64.exe
```

## 源码公开与非商业许可

智慧窗采用自定义的“源码公开、限制商业使用”许可，不属于 OSI 认可的开源许可证。

项目名称、Logo、原创源代码、界面设计、原创文档和相关品牌素材归 **Sendyun Technology Unit** 与原作者 **Mr.Dove** 所有。

允许用户为非商业目的研究、修改和优化已下载版本，但必须遵守以下要求：

- 修改版版本号必须添加 `【OT】` 后缀，例如 `1.0.1【OT】个人优化版`；
- 不得冒充 Sendyun 官方版本；
- 不得删除版权声明；
- 未经书面许可，不得将程序、源码或修改版用于任何商业用途。

完整条款请阅读 [LICENSE.md](LICENSE.md)。

## 开机自启动

打开智慧窗控制台，在控制中心底部开启“开机自启动”。程序会写入当前用户的 Windows Run 启动项，并在登录后以后台方式启动。

关闭该选项会删除对应启动项，不需要管理员权限。

## 从源码构建

进入 `源码` 目录并运行：

```powershell
.\build.ps1
```

构建需要 Windows 自带的 .NET Framework 4.x C# 编译器。

生成 Microsoft Store 使用的 x64 安装程序：

```powershell
.\installer\build-installer.ps1
```

安装器源码位于 `installer` 目录。

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

