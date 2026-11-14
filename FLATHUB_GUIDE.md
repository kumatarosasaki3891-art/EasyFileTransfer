# Flathub / Flatpak パッケージ化ガイド

このドキュメントでは、**EasyFileTransfer** を Flathub に公開するため、およびローカルで Flatpak パッケージとしてビルド・テストするための手順を説明します。

---

## 1. 作成・配置されたファイル一覧

| ファイル名 | 役割 |
| :--- | :--- |
| `io.github.kumatarosasaki3891_art.EasyFileTransfer.yaml` | Flatpak ビルドマニフェスト（ソースからビルド・タグおよびコミット固定） |
| `io.github.kumatarosasaki3891_art.EasyFileTransfer.metainfo.xml` | AppStream メタ情報（Flathub ストア表示・日英対応・v1.1.0） |
| `io.github.kumatarosasaki3891_art.EasyFileTransfer.desktop` | デスクトップエントリ（アプリランチャー用） |
| `nuget-sources.json` | Flathub オフラインビルド用の NuGet パッケージ依存定義（31パッケージ） |
| `docs/screenshots/` | AppStream メタデータが参照するスクリーンショット実体画像 |

### Application ID について
Flathub の公式ガイドライン（リバースDNS形式およびコードホスティング命名規則）に基づき、以下の ID を採用しています。
- **ID**: `io.github.kumatarosasaki3891_art.EasyFileTransfer`
- **理由**: GitHub のユーザー名に含まれるハイフン（`-`）は、Flathub / AppStream の自動検証ルールによりアンダースコア（`_`）へ置換されます（Flathub 側で自動的に `https://github.com/kumatarosasaki3891-art/EasyFileTransfer` と照合されます）。

### finish-args（付与権限）について
- `--share=network`: ローカル Wi-Fi（同一LAN）内での HTTP サーバー起動およびファイル送受信に必須。
- `--filesystem=xdg-download`: 受信ファイルの保存先（初期値: `~/Downloads/EasyFileTransfer`）および共有ファイルの読み込みに必須。
- `--socket=wayland`, `--socket=fallback-x11`, `--device=dri`: Avalonia UI の GUI 描画（Wayland / X11）。
- `--talk-name=org.freedesktop.portal.Desktop`: ネイティブファイル選択ダイアログや `xdg-open` のポータル連携。

---

## 2. ローカルでのビルドと動作確認（Linux / WSL2 環境）

### 必要ツールのインストール (Ubuntu / Debian 例)
```bash
sudo apt update
sudo apt install flatpak flatpak-builder
flatpak remote-add --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo

# ランタイムとSDK、.NET 10 SDK拡張機能のインストール
flatpak install flathub org.freedesktop.Platform//24.08
flatpak install flathub org.freedesktop.Sdk//24.08
flatpak install flathub org.freedesktop.Sdk.Extension.dotnet10//24.08
```

### ローカルソースからのビルド手順
手元の作業ディレクトリから直接ビルドする場合は、マニフェストの `sources` を以下のように一時変更できます:

```yaml
    sources:
      - type: dir
        path: .
      - nuget-sources.json
```

ビルドおよびローカルインストール:
```bash
flatpak-builder --user --install --force-clean build-dir io.github.kumatarosasaki3891_art.EasyFileTransfer.yaml
```

アプリの起動確認:
```bash
flatpak run io.github.kumatarosasaki3891_art.EasyFileTransfer
```

---

## 3. Flathub への申請手順（公式リポジトリへのプルリクエスト）

Flathub にアプリケーションを公開する際は、Flathub の [flathub/flathub](https://github.com/flathub/flathub) リポジトリに新規アプリケーションのプルリクエスト（PR）を送信します。

### ステップ 1: GitHub へのタグ付けと Push
```bash
git push origin main
git tag v1.1.0
git push origin v1.1.0
```

### ステップ 2: Flathub 申請用 PR の作成
1. GitHub で [flathub/flathub](https://github.com/flathub/flathub) をフォークします。
2. 新しいブランチを作成します（ブランチ名例: `io.github.kumatarosasaki3891_art.EasyFileTransfer`）。
3. 以下のファイルを配置してコミットします。
   - `io.github.kumatarosasaki3891_art.EasyFileTransfer.yaml`
   - `nuget-sources.json`
4. `flathub/flathub` の `new-pr` ブランチに向けてプルリクエストを作成します。
5. Flathub のビルドBotが自動検証テストを行い、レビュー後にマージされると専用の Flathub リポジトリが自動開設され、Flathub ストアに公開されます。
