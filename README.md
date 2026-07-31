<p align="center">
  <img src="assets/logo.svg" width="112" alt="Bazaar Hover Wiki logo">
</p>

<h1 align="center">Bazaar Hover Wiki</h1>

<p align="center">
  <a href="https://github.com/zhuhaichao518/BazaarHoverWiki/actions/workflows/ci.yml"><img src="https://github.com/zhuhaichao518/BazaarHoverWiki/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-E4B45B.svg" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/platform-Windows-4D8AC9.svg" alt="Windows">
</p>

一个只做一件事的《The Bazaar》外部伴随工具：

> 鼠标停在游戏选项名称附近 → 按 `F` → 截取鼠标周围画面 → Windows 本地 OCR → 在悬浮窗中搜索 BazaarDB Wiki。

> [!WARNING]
> 这是非官方社区项目，与 Tempo Games、The Bazaar 或 BazaarDB 没有关联或背书。官方曾明确反对修改游戏客户端；纯外部 OCR 悬浮工具的许可边界仍不够清晰，请在使用前自行查阅最新 EULA 和官方规则。

## 功能

- 仅在按下 `F` 时识别鼠标附近的英文/中文文本，后台不会自动扫描。
- 在独立置顶窗口中显示 BazaarDB 搜索结果。
- 按 `D` 显示/隐藏悬浮窗。
- 悬浮窗不会抢占系统焦点，可直接拖动标题栏并缩放。
- 悬浮窗被排除在本地 OCR 截图之外，按 `F` 搜索时无需隐藏或闪烁。
- 支持手动修正 OCR 搜索词。

## 安全边界

- 不向游戏进程注入 DLL。
- 不修改游戏文件。
- 不读取游戏内存、网络流量或账号信息。
- 不需要管理员权限。
- 不上传截图；OCR 完全使用 Windows 本地能力。
- 唯一的联网行为是内嵌浏览器访问 `bazaardb.gg`；其他域名跳转会被阻止。
- 没有遥测、自动更新或后台常驻服务。

更完整的说明见 [安全模型](docs/SECURITY-MODEL.md) 和 [安全政策](SECURITY.md)。

## 运行

### 下载发布版

从 [Releases](https://github.com/zhuhaichao518/BazaarHoverWiki/releases) 下载 Windows 压缩包，校验同版本的 `SHA256SUMS.txt` 后解压运行。

### 从源码运行

需要 Windows 11、.NET 9 SDK 和 WebView2 Runtime（Windows 11 通常已经自带）。

```powershell
dotnet run --project .\BazaarHoverWiki.csproj
```

快捷键：

- `F`：重新识别鼠标附近并搜索 Wiki。
- `D`：显示/隐藏 Wiki 窗口。
- `F9`：暂停/恢复插件；暂停时会取消 OCR、隐藏 Wiki，并注销 `F`、`D` 热键。

首次调试时，将鼠标放在任意英文文本附近并按 `F` 即可测试。

## 配置

编辑输出目录中的 `settings.json`：

- `captureWidth` / `captureHeight`：鼠标周围截图范围。
- `preferredOcrLanguages`：优先使用的 Windows OCR 语言包。
- `wikiSearchUrl`：Wiki 搜索地址，必须包含 `{query}`。

## 当前 MVP 的限制

- OCR 结果只保留 Unicode 字母和数字，删除所有空白及箭头、星号、圆点、括号等特殊符号，并忽略“奖励”标签。
- OCR 会根据鼠标在屏幕中的位置向 tooltip 方向偏移截图区域，覆盖出现在鼠标上方或侧面的标题。
- 候选先过滤数字、效果句、物品类型标签和说明词条，再按字号、标题位置及距离选择道具主要名称。
- OCR 会采样候选文字框颜色；蓝色/青色占比高的类型和效果词条会被过滤，混色候选会降权。
- OCR 仍可能把按钮价格、描述或背景文字当作标题；此时可以在主窗口手动修正搜索词。
- 无法保证识别由其他模组生成的中文译名，因为 BazaarDB 主要使用英文名称。
- BazaarDB 使用 Cloudflare；首次加载可能需要完成浏览器验证。
- 全屏独占模式可能无法显示普通 Windows 悬浮窗，建议使用无边框窗口模式。

## 构建单文件版本

```powershell
dotnet publish .\BazaarHoverWiki.csproj -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true
```

输出位于 `bin\Release\net9.0-windows10.0.26100.0\win-x64\publish`。

## 许可

本项目自身代码使用 MIT License。`The Bazaar`、BazaarDB 及相关名称和内容归各自权利人所有。

欢迎贡献，提交前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。架构说明见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。
