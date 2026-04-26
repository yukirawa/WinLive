# localhost API

WinLive は、外部ツールやアプリから進捗つき Live Activity を送るための localhost API を持っています。

## 基本

既定の URL:

```text
http://127.0.0.1:8765
```

認証:

```text
Authorization: Bearer <token>
```

トークンは初回起動時に生成され、設定画面で確認・再生成できます。

## エンドポイント

```text
GET    /api/v1/health
GET    /api/v1/activities
PUT    /api/v1/activities/{id}
PATCH  /api/v1/activities/{id}
DELETE /api/v1/activities/{id}
```

## PowerShell 例

```powershell
$token = "<settings window token>"
$headers = @{ Authorization = "Bearer $token" }

Invoke-RestMethod `
  -Method Put `
  -Uri "http://127.0.0.1:8765/api/v1/activities/demo-download" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body '{
    "type": "download",
    "state": "active",
    "title": "Demo download",
    "subtitle": "42%",
    "progress": 0.42,
    "priority": 40,
    "sourceApp": { "name": "PowerShell" }
  }'
```

## デモクライアント

```powershell
dotnet run --project tools/WinLive.ApiDemo -- --token "<settings window token>"
```

シナリオを指定できます。

```powershell
dotnet run --project tools/WinLive.ApiDemo -- --token "<token>" --scenario download
dotnet run --project tools/WinLive.ApiDemo -- --token "<token>" --scenario encode
dotnet run --project tools/WinLive.ApiDemo -- --token "<token>" --scenario all
```

対応シナリオ:

- `download`
- `upload`
- `encode`
- `copy`
- `timer`
- `install`
- `all`

継続的な更新は、同じ `id` に対して最初に `PUT`、以後に `PATCH` を送る形を推奨します。

```powershell
Invoke-RestMethod `
  -Method Patch `
  -Uri "http://127.0.0.1:8765/api/v1/activities/demo-download" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body '{
    "subtitle": "75%",
    "progress": 0.75
  }'
```

## LiveActivity の主な項目

- `id`: activity の一意 ID
- `type`: `media`, `download`, `upload`, `encode`, `fileCopy`, `timer`, `install`, `genericProgress`, `experimental` など
- `state`: `active`, `paused`, `completed`, `error` など
- `title`: 1 行目の表示文字列
- `subtitle`: 2 行目の表示文字列
- `progress`: `0.0` から `1.0`
- `priority`: 表示優先度
- `sourceApp`: 送信元アプリ情報
- `media`: メディア用の追加情報
- `metadata`: 任意のキー/値
