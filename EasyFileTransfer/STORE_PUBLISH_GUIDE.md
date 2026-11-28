# EasyFileTransfer - Microsoft Store 申請および MSIX ビルドガイド

本ドキュメントは、**EasyFileTransfer** を Microsoft Store 向け MSIX パッケージとしてビルドし、Partner Center へ申請・公開するためのガイドです。
本アプリは **完全無料（アプリ内課金・有料機能なし）** として設計・構成されています。

---

## 1. Microsoft Store 登録情報

Partner Center で予約された製品識別情報です（公開用に実際のID・発行者情報は `XXXX` に伏せています。ご自身の環境に合わせて書き換えてください）：

| 項目 | 設定値 |
| :--- | :--- |
| **表示名 (DisplayName)** | `簡単ファイル転送` |
| **Package/Identity/Name** | `XXXXXXXX.XXXXXXXXXXXXX` |
| **Package/Identity/Publisher** | `CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX` |
| **Package/Properties/PublisherDisplayName** | `XXXX` |
| **Package Family Name (PFN)** | `XXXXXXXX.XXXXXXXXXXXXX_XXXXXXXXXXXXX` |
| **Package SID** | `S-1-15-2-XXXXXXXXXX-XXXXXXXXXX-XXXXXXXXX-XXXXXXXXXX-XXXXXXXXX-XXXXXXXXXX-XXXXXXXXXX` |
| **Microsoft Store ID** | `XXXXXXXXXXXX` |
| **ディープ リンク (公開後)** | `ms-windows-store://pdp/?productid=XXXXXXXXXXXX` |
| **Web ストア URL (公開後)** | `https://apps.microsoft.com/detail/XXXXXXXXXXXX` |
| **課金設定** | **完全無料（課金・アプリ内購入なし）** |

---

## 2. ビルドファイルと構成

| ファイル | 役割 |
| :--- | :--- |
| [`EasyFileTransfer/Platforms/Windows/Package.appxmanifest`](Platforms/Windows/Package.appxmanifest) | ストア登録ID（`XXXXXXXX.XXXXXXXXXXXXX`）とパブリッシャー（`XXXX`）、LAN通信に必要な権限を設定 |
| [`EasyFileTransfer/EasyFileTransfer.csproj`](EasyFileTransfer.csproj) | アプリID、バージョン、Windows Release最適化（ReadyToRun）を設定 |
| [`build-msix-easyfiletransfer.ps1`](../build-msix-easyfiletransfer.ps1) | Store申請用 / Sideloadテスト用のMSIXビルドスクリプト |
| [`build-msix-easyfiletransfer.cmd`](../build-msix-easyfiletransfer.cmd) | エクスプローラーからダブルクリックでビルドできる対話型ランチャー |
| [`EasyFileTransfer/build-msix.ps1`](build-msix.ps1) | プロジェクトフォルダ内から直接実行可能なラッパースクリプト |

---

## 3. MSIX パッケージのビルド方法

### 方法 A. ダブルクリックで実行（一番簡単）
プロジェクトルートの `build-msix-easyfiletransfer.cmd` をダブルクリックし、メニューから選択します：
- `1`: **Microsoft Store 申請用パッケージ** を生成（デフォルト）
- `2`: **Sideload (ローカルテスト) 用パッケージ** を生成（署名付き）

### 方法 B. PowerShell コマンドで実行

#### 1. Microsoft Store 申請用ビルド (未署名・StoreUpload モード)
```powershell
.\build-msix-easyfiletransfer.ps1 -Mode Store
```
（別の出力先を指定したい場合は `-OutputDir "C:\path\to\dir"` を指定できます）

またはプロジェクトフォルダ内のラッパー `EasyFileTransfer\build-msix.ps1` を使用：
```powershell
.\EasyFileTransfer\build-msix.ps1 -Mode Store
```
- **生成先**:
  `dist\EasyFileTransfer_1.1.2.0_x64.msix`
  （※ Microsoft Store の審査パイプラインへアップロードするファイルです）

#### 2. ローカルテスト用ビルド (自己署名付き Sideload)
※ セキュリティ上の理由からリポジトリに署名用証明書（`Package_TemporaryKey.pfx` / `Package.cer`）は同梱していません。Sideloadビルドを行う場合は、ルート直下に自己署名証明書を配置してから実行してください。
```powershell
.\build-msix-easyfiletransfer.ps1 -Mode Sideload
```
- **生成先**:
  `dist\Sideload\EasyFileTransfer_1.1.2.0_x64.msix`
  および同フォルダ内に `EasyFileTransfer.cer`（公開証明書）が出力されます。
- **ローカルインストール手順**:
  1. `EasyFileTransfer.cer` をダブルクリック
  2. 「証明書のインストール」をクリック
  3. 保存場所に「ローカル コンピューター」を選択
  4. 証明書ストアに「信頼されたルート証明機関」を指定して完了
  5. `EasyFileTransfer_1.1.2.0_x64.msix` をダブルクリックしてインストール

---

## 4. Microsoft Partner Center での申請手順 (v1.1 アップデート)

1. [Microsoft Partner Center ダッシュボード](https://partner.microsoft.com/dashboard) にサインインします。
2. アプリ一覧から **EasyFileTransfer**（Store ID: `XXXXXXXXXXXX`）を選択し、「申請の更新」または新しい申請を開きます。
3. **「パッケージ」ページ**:
   - `dist\EasyFileTransfer_1.1.2.0_x64.msix` をドラッグ＆ドロップしてアップロードします。
   - 既存の v1.0 パッケージから v1.1 へ更新されます。
4. **「価格と提供状況」ページ**:
   - 基本価格: **「無料」** を選択
   - アプリ内購入（アドオン）: **なし**
5. **「プロパティ」ページ**:
   - カテゴリ: 「ユーティリティとツール」などを選択
   - プライバシーポリシー: ローカルネットワーク内完結型アプリである旨
6. **「ストアの登録情報」ページ**:
   - 説明文、機能、スクリーンショットを登録
7. すべての項目を入力後、**「審査に提出」** をクリックします。
