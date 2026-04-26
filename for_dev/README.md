# WinLive 開発者向けドキュメント

このフォルダには、WinLive の開発・保守・リリースに必要な情報を分けて置いています。
ユーザー向けの説明はルートの `README.md` を参照してください。

## ドキュメント一覧

- [ビルドとリリース](build-release.md)
- [API 仕様](api.md)
- [内部構成](architecture.md)
- [UI と挙動](ui-behavior.md)
- [テスト](testing.md)
- [既知の制限](known-limits.md)

## よく使うコマンド

```powershell
dotnet build WinLive.slnx
dotnet test WinLive.slnx
dotnet run --project WinLive.App
```

単一 exe の発行:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-onefile.ps1 -Version 1.0.0
```

