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

## LiveActivity の主な項目

- `id`: activity の一意 ID
- `type`: `media`, `download`, `upload`, `encode`, `install`, `experimental` など
- `state`: `active`, `paused`, `completed`, `error` など
- `title`: 1 行目の表示文字列
- `subtitle`: 2 行目の表示文字列
- `progress`: `0.0` から `1.0`
- `priority`: 表示優先度
- `sourceApp`: 送信元アプリ情報
- `media`: メディア用の追加情報
- `metadata`: 任意のキー/値

