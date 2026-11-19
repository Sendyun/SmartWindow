# Microsoft Store MSIX

The Store package uses the Partner Center product identity:

```text
Package/Identity/Name:
SendyunTechnologyUnit.SmartWindow

Package/Identity/Publisher:
CN=4591EE90-EA8E-423D-84B2-08FC9222DF54

PublisherDisplayName:
Sendyun Technology Unit
```

Build requirements:

- Windows SDK `makeappx.exe`
- Windows SDK `signtool.exe`
- .NET Framework 4.x

Run:

```powershell
.\build-msix.ps1
```

The script creates:

- `SmartWindow_1.1.2.0_x64.msix`
- `SmartWindow_1.1.2.0_x64.msixupload`

The `.msixupload` file is the file to select in Partner Center.

