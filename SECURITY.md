# Security Policy

## Supported versions

项目处于早期开发阶段，仅最新 GitHub Release 接收安全更新。

## Reporting a vulnerability

请不要为可利用的漏洞直接创建公开 Issue。使用仓库的 GitHub Security Advisory：

1. 打开仓库的 **Security** 页面。
2. 选择 **Report a vulnerability**。
3. 描述受影响版本、复现步骤、影响和建议修复方式。

我们会尽量在 7 天内确认报告，并在修复可用后协调披露。

## Security promises

- 程序不要求管理员权限。
- 不读取游戏进程内存或网络流量。
- 截图仅在内存中交给 Windows OCR，不写盘、不上传。
- 内嵌浏览器只允许导航到 `bazaardb.gg` 及其子域。
- 发布工作流生成 SHA-256 校验文件。

这些承诺是项目的安全边界；偏离它们的改动必须经过公开设计讨论。
