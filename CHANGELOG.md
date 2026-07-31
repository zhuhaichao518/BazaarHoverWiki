# Changelog

本项目遵循 [Semantic Versioning](https://semver.org/)。

## [Unreleased]

## [0.2.0] - 2026-08-01

### Added

- `F` 手动 OCR 搜索、`D` 显示/隐藏 Wiki、`F9` 暂停/恢复插件。
- 可拖动、缩放且不抢占游戏焦点的 Wiki 窗口。
- 基于文字颜色、字号、位置和文本特征的道具主名称评分。

### Changed

- 改为纯手动识别模式：后台不再定时扫描。
- 截图区域扩大至 `1500×900`，水平居中并重点覆盖鼠标上方。
- OCR 结果只保留 Unicode 字母和数字，并过滤类型标签、效果句及蓝色/青色词条。
- 每次按 `F` 都强制执行新的 WebView2 导航，确保标题和 BazaarDB 搜索结果同步更新。

### Fixed

- 修正两字中文道具名被错误降权的问题。
- Wiki 窗口从 OCR 截图中排除，按 `F` 时不再隐藏闪烁或识别网页自身。

## [0.1.0] - 2026-07-31

### Added

- Windows 外部截图与本地 OCR。
- 基于鼠标距离的文本候选排序。
- BazaarDB 悬浮浏览器和手动搜索。
- 全局快捷键与鼠标穿透模式。
- CI、CodeQL 和可校验的 Release 构建。

[Unreleased]: https://github.com/zhuhaichao518/BazaarHoverWiki/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/zhuhaichao518/BazaarHoverWiki/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/zhuhaichao518/BazaarHoverWiki/releases/tag/v0.1.0
