# ビルドとリリース

## 前提

- Windows 11 優先
- .NET SDK `10.0.202`
- WPF を使うため Windows 環境が必要

SDK バージョンは `global.json` で固定しています。

## 通常ビルド

```powershell
dotnet build WinLive.slnx
```

## テスト

```powershell
dotnet test WinLive.slnx
```

## ローカル起動

```powershell
dotnet run --project WinLive.App
```

既に WinLive が起動していると、ビルド時に dll や exe がロックされることがあります。
その場合はタスクトレイから WinLive を終了してから再実行してください。

## 単一 exe 発行

既定では、.NET ランタイム込みの self-contained 単一 exe を作ります。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-onefile.ps1 -Version 1.0.0
```

出力先:

```text
artifacts\publish\win-x64\WinLive_v1.0.0.exe
```

バージョンを変える場合:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-onefile.ps1 -Version 1.0.1
```

出力ファイル:

```text
WinLive_v1.0.1.exe
```

v1.1.0 を発行する場合:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-onefile.ps1 -Version 1.1.0
```

## framework-dependent 発行

対象 PC に .NET 10 Desktop Runtime を入れてもらう前提なら、より小さい exe を作れます。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-onefile.ps1 -FrameworkDependent -Version 1.0.0
```

## 実行ポリシー

PowerShell で `running scripts is disabled` が出る場合は、次のように実行します。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-onefile.ps1
```
