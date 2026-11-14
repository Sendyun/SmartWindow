# SmartWindow Installer Exit Codes

This document describes the EXE return codes produced by
`SmartWindow-Setup-x64.exe`.

## Standard outcomes

| Outcome | Return code |
| --- | ---: |
| Installation or uninstallation succeeded | `0` |
| User canceled installation | `1602` |
| Another SmartWindow installation is already running | `1618` |
| The destination disk is full | `112` |
| Any other installation failure | `1603` |

An existing installation is upgraded in place and returns `0`.

The installer does not require a restart, does not perform network
operations, and does not use a separate installer runtime that can report
additional application-specific codes.

## Other failures

Return code `1603` indicates an unexpected file, permission, extraction,
shortcut, or uninstallation error. The installer may display additional
details in interactive mode.

For support, contact:

- Author: Mr.Dove
- Publisher: Sendyun Technology Unit
- Email: Mr.Dove@aliyun.com
- Repository: https://github.com/Sendyun/SmartWindow

