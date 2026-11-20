# QSL 卡片工坊

面向业余无线电台友的 Windows 双面 QSL 卡片编辑器。可以套用横版或竖版模板，也可以从空白卡面开始制作。

## 下载与安装

- [Windows x86 安装包](https://github.com/BD3QWS/QSLStudio/releases/download/v1.10.5/QSLStudio-1.10.5-Setup-x86.exe)
- [SHA-256 校验文件](https://github.com/BD3QWS/QSLStudio/releases/download/v1.10.5/QSLStudio-1.10.5-Setup-x86.exe.sha256)
- [发行版与更新说明](https://github.com/BD3QWS/QSLStudio/releases)

主要使用环境为 Windows 10 和 Windows 11。安装程序需要管理员确认，目前未签名。Windows XP SP3 与 .NET Framework 4.0 是旧系统构建目标，本版本在 Windows 11 上验证。

启动后可新建或打开工程，按 **F1** 阅读内置离线手册。关闭旧版程序后，可沿用安装目录覆盖升级。

## 主要功能

- 18 套双面模板，支持自定义成品尺寸、文字和图层编辑、底图裁切与自由绘制。
- 输出供厂家印刷的留空底版。成品卡可按原尺寸套印通联资料，并以毫米为单位校准位置。
- 导出 PDF、PNG、JPG。支持导入 ADIF 日志，以及保存可继续编辑的 `.qslcard` 工程。
- 为 PCB、木片、亚克力和金属卡设计卡面并导出加工文件。

更多操作说明见[用户手册](QSLStudio/UserManual.txt)。

## 源码构建

在安装了 .NET Framework 的 Windows 开发机上准备 `Microsoft.NETFramework.ReferenceAssemblies.net40` 引用程序集，然后运行以下命令。编辑器默认生成到 `QSLStudio/dist`。

```powershell
.\QSLStudio\Build.ps1 -FrameworkRefs 'C:\path\to\build\.NETFramework\v4.0'
```

[构建说明](QSLStudio/BUILD.txt) | [安装器源码](Installer/QSLStudio.nsi)

## 许可

源码采用 [MIT](LICENSE) 许可，原创空白卡片版式采用 CC0 1.0。随附协会标志的署名、来源和许可见[素材说明](QSLStudio/ASSETS.txt)。
