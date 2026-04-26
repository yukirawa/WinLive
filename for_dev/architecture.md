# 内部構成

WinLive は、WPF アプリ本体、Core モデル、Windows 連携、テスト、API デモに分かれています。

## プロジェクト

```text
WinLive.App
src/WinLive.Core
src/WinLive.Windows
tests/WinLive.Tests
tools/WinLive.ApiDemo
```

## WinLive.App

WPF の UI とアプリ起動処理を担当します。

主なファイル:

- `App.xaml.cs`: 起動、例外表示、バックエンド生成
- `MainWindow.xaml`: 島 UI
- `MainWindow.xaml.cs`: 表示アニメーション、ドラッグ移動
- `WinLiveShellViewModel.cs`: 表示状態、コマンド、配置状態
- `SettingsWindow.xaml`: 設定画面
- `WpfTaskbarPlacementService.cs`: WPF 座標での表示位置補正

## WinLive.Core

OS や UI に依存しないモデルとインターフェイスを置きます。

主な責務:

- `LiveActivity` モデル
- activity store
- settings モデル
- command router / placement / settings store などの抽象化

## WinLive.Windows

Windows 固有の連携を担当します。

主な責務:

- Global System Media Transport Controls からのメディア取得
- localhost API サーバー
- Shell_NotifyIcon によるトレイ常駐
- UI Automation による実験的な進捗検出
- AppData への設定保存

## 表示の基本方針

通常時は最優先 activity を 1 件だけ表示します。
展開時は、追加 activity を同じサイズのタイルとして上下どちらかへ並べます。

