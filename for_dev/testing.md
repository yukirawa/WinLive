# テスト

## 自動テスト

```powershell
dotnet test WinLive.slnx
```

主に次を確認しています。

- activity store の優先順位
- progress の clamp
- 表示 / 非表示状態
- 設定保存
- API auth / CRUD
- 配置補正
- 全画面抑制
- ドラッグ位置保存
- 上方向展開時の位置計算

## 手動確認

リリース前に確認したい項目:

- Spotify などのメディア再生中に島が出る
- 再生 / 一時停止 / 前後操作が動く
- 島をクリックして展開できる
- `Up` / `Down` の展開方向が設定通りになる
- 島をドラッグして移動できる
- トレイの `Reset position` で位置が戻る
- 全画面動画やゲーム中に島が非表示になる
- localhost API で progress activity を作れる
- WinLive.ApiDemo が動く
- 発行スクリプトで `WinLive_vX.Y.Z.exe` が生成される

## 診断ログ

通常はログを抑えています。
必要な場合だけ次の環境変数を付けて起動します。

```powershell
$env:WINLIVE_DIAGNOSTICS = "1"
dotnet run --project WinLive.App
```

