# EasyFileTransfer.Linux (簡単ファイル転送 Linux/クロスプラットフォーム版)

Avalonia UI (.NET 10) を使用した、スマホとPC間でQRコード経由で双方向に直接ファイル送受信を行うデスクトップアプリケーションです。

## 特徴とアーキテクチャ

- **コアロジックの完全共有**:
  - `FileTransferServer.cs`（UAC管理者権限不要の軽量HTTPサーバー、1:1使い捨てセッション認証）
  - `Models/ReceivedFileInfo.cs` / `SharedFileInfo.cs`
  - `QrCodeGenerator.cs`（SkiaSharp描画）
  - `LocalizationService.cs`（11言語対応の多言語ローカリゼーション）
  を Windows 版（`EasyFileTransfer`）から直接リンク参照して共有しています。
- **Linux ネイティブ最適化 (`StorageSettings.cs`)**:
  - 保存フォルダー: `~/Downloads/EasyFileTransfer`
  - 共有フォルダー: `~/Downloads/EasyFileTransfer/Shared`
  - 設定永続化: `~/.config/EasyFileTransfer/config.json` (XDG Base Directory 準拠)
  - ファイラー・ファイル起動: `xdg-open`
  - フォルダー/ファイル選択ダイアログ: Avalonia `StorageProvider` (ネイティブGTK/Portal連携)

## ビルド・実行方法

### 開発時ビルド・起動
```bash
dotnet run --project EasyFileTransfer.Linux/EasyFileTransfer.Linux.csproj
```

### Linux向け単一バイナリの発行 (Self-Contained)
```bash
dotnet publish EasyFileTransfer.Linux/EasyFileTransfer.Linux.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true
```
発行された実行ファイルは `EasyFileTransfer.Linux/bin/Release/net10.0/linux-x64/publish/EasyFileTransfer.Linux` に出力されます。
