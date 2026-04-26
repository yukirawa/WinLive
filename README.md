# WinLive

WinLive は、Windows 11 向けのライブ通知アプリです。
音楽再生、ダウンロード、エンコードなど、いま進行中の情報を小さな「島」として表示します。

スマートフォンの Dynamic Island / Hyper Island / Aqua Dynamics の考え方を参考にしつつ、PC の通知領域やタスクバーに合うように作っています。

## できること

- 再生中のメディアを小さな島に表示
- 再生 / 一時停止、前後トラックの操作
- localhost API から進捗つきのライブ通知を表示
- ダウンロード、アップロード、エンコード、コピー、タイマー、インストールなどの継続更新を表示
- 実験的な自動進捗検出で、既存アプリの ProgressBar を島に表示
- 島をクリックして展開
- Aqua Dynamics 風に、同じ大きさのタイルが上または下へ展開
- 島の大きさを Small / Medium / Large から選択
- 島をドラッグして好きな位置へ移動
- トレイアイコンから設定、位置リセット、終了
- 全画面動画やゲーム中は自動で非表示

## 使い方

1. `WinLive_v1.0.0.exe` のような WinLive の exe を起動します。
2. タスクトレイに WinLive のアイコンが表示されます。
3. 音楽を再生したり、対応する進捗通知が入ると島が表示されます。
4. 島をクリックすると展開します。
5. 島本体をドラッグすると位置を変更できます。

島が邪魔になった場合は、タスクトレイの WinLive アイコンから `Reset position` を選ぶと初期位置に戻せます。

API の動作確認にはデモクライアントを使えます。

```powershell
dotnet run --project tools/WinLive.ApiDemo -- --token "<settings window token>" --scenario all
```

## 設定

タスクトレイの WinLive アイコンから設定を開けます。

主な設定:

- 全画面中に非表示にする
- 一時停止中のメディアも表示する
- アルバムアートを表示する
- 島サイズを `Small` / `Medium` / `Large` から選ぶ
- 島の展開方向を `Up` / `Down` から選ぶ
- localhost API の有効化
- API トークンの確認と再生成
- デモ activity の送信
- 実験的な進捗検出の有効化

設定変更の一部は、WinLive の再起動後に反映されます。

## 配布ファイル名

リリース用 exe は、バージョンが分かる名前にできます。

例:

```text
WinLive_v1.0.0.exe
WinLive_v1.0.1.exe
WinLive_v1.2.0.exe
```

## 注意点

- WinLive は Windows 11 優先で作っています。
- 署名していないベータ版では、Windows SmartScreen の警告が出る場合があります。
- すべてのアプリのタスクバー進捗を読み取れるわけではありません。
- 実験的な進捗検出は、アプリによって検出できない場合があります。
- 表示する情報がないとき、島は非表示になり、タスクトレイにだけ常駐します。

## 開発者向け情報

ビルド方法、API 仕様、内部構成、テスト、既知の制限は `for_dev` フォルダに分けてあります。

- [開発者向け目次](for_dev/README.md)
- [ビルドとリリース](for_dev/build-release.md)
- [API 仕様](for_dev/api.md)
- [内部構成](for_dev/architecture.md)
- [UI と挙動](for_dev/ui-behavior.md)
- [テスト](for_dev/testing.md)
- [既知の制限](for_dev/known-limits.md)
