# Contributing

感谢你帮助改进 Bazaar Hover Wiki。

## 开发原则

1. 保持纯外部实现：不得注入、修改或替换游戏文件。
2. 不加入游戏内存读取、网络拦截、自动操作或账号数据采集。
3. 不提交从游戏包提取的资源、卡牌图片、音频或专有数据。
4. 新的网络请求必须在 PR 中说明目标域名、数据内容和必要性。
5. 默认不加入遥测；任何遥测提案都必须明确选择加入并经过单独讨论。

## 本地检查

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet format --verify-no-changes --no-restore
```

## Pull Request

- 一个 PR 只解决一个清晰问题。
- 描述用户影响、安全影响和验证方式。
- UI 变更请附截图。
- 依赖升级必须同时更新 `packages.lock.json`。

提交代码即表示你同意按仓库的 MIT License 授权你的贡献。
