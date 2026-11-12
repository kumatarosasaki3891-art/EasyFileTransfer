# EasyFileTransfer（簡単ファイル転送）

スマホとPCの間で、QRコードを読み取るだけでファイルを双方向にやり取りできるローカルファイル転送ツールです。  
外部サーバーやインターネット回線を経由せず、同じ Wi-Fi（LAN）内だけで直接通信する仕組みになっています。

> 初めてオープンソースとして公開してみます。  
> 勉強しながら組んだコードなので拙い部分もあるかと思いますが、同じようなツールを作ってみたい方の参考になれば嬉しいです！

---

## 主な特徴

- **スマホ側に専用アプリ不要**
  - PC側で表示したQRコードをスマホのカメラで読み取ると、ブラウザ上で送受信用のWeb画面が開きます。
- **双方向ファイル転送**
  - **受信モード（スマホ → PC）**: スマホからファイル選択・カメラ撮影・ドラッグ＆ドロップでPCへ送信できます（1ファイル最大 2GB まで）。同名ファイルがある場合は自動で `(1)` `(2)` を付けて別名保存します。
  - **送信モード（PC → スマホ）**: PCの共有フォルダーに入れたファイルを、スマホ側のブラウザから一覧表示・ダウンロードできます。
- **管理者権限不要の軽量HTTPサーバー (`FileTransferServer.cs`)**
  - `HttpListener` ではなく `TcpListener` ベースで実装しているため、Windows で UAC 管理者権限や複雑な `netsh` 設定なしで起動できます（デフォルトポート `5050`、使用中なら空きポートを自動探索）。
- **1:1 使い捨てセッション保護**
  - ランダムな8桁のセッショントークン（有効期限30分）を発行し、最初に接続した端末のIPアドレスに紐づけることで、同じネットワーク内の別端末からのアクセスをブロックします。
- **QRコードの自前生成 (`QrCodeGenerator.cs`)**
  - 外部のQR生成ライブラリを増やさずに動かしたかったため、`SkiaSharp` を使って ISO/IEC 18004（Version 1〜7）のQRコード描画を実装しています。
- **11言語対応 (`LocalizationService.cs`)**
  - PC側UI・スマホ側Web画面ともに、日本語・英語・簡体字中国語・韓国語・繁体字中国語・ベトナム語・スペイン語・ドイツ語・フランス語・ポルトガル語・イタリア語の11言語切り替えに対応しています。
- **Windows版 (.NET MAUI) & Linux版 (Avalonia UI)**
  - モデルとサーバー/QR/多言語ロジックを共有し、UI部分だけをプラットフォームごとに作っています。

---

## 使い方

1. PC とスマホを**同じ Wi-Fi（同一ネットワーク）**に接続します。
2. PC でアプリを起動します。
3. **スマホ → PC に送りたいとき**
   - 「受信 (スマホ → PC)」タブを開き、スマホの標準カメラでQRコードをスキャンします。
   - 開いたWeb画面で写真やファイルを選んで送信すると、PCの保存先フォルダー（初期値: `Downloads/EasyFileTransfer`）へ保存されます。
4. **PC → スマホ に送りたいとき**
   - 「送信 (PC → スマホ)」タブを開き、「＋ ファイルを追加」ボタン（または共有フォルダー `Downloads/EasyFileTransfer/Shared` への直接配置）でファイルを用意します。
   - スマホでQRコードをスキャンして、表示された一覧からダウンロードします。

> **つながらないときのヒント**
> - PCに仮想ネットワーク（WSL / Docker / Hyper-V / VPN など）があると別IPが選ばれることがあるため、画面の「🔄 IP切替」ボタンを試してみてください。
> - ルーターの「プライバシーセパレーター（AP分離）」が有効になっていると端末同士が通信できないため、無効化されているかご確認ください。
> - Windows ファイアウォール等の確認が出た場合は、プライベートネットワークでの通信を許可してください。

---

## ディレクトリ構成

```text
EasyFileTransfer/
├── EasyFileTransfer/                  # Windows 向けアプリ (.NET MAUI / net10.0-windows10.0.19041.0)
│   ├── Models/                        # 受信・共有ファイル情報のモデル (Linux版と共有)
│   ├── Services/                      # HTTPサーバー・QR生成・多言語・フォルダー設定
│   ├── Platforms/                     # プラットフォーム別エントリーポイント / Package.appxmanifest
│   ├── MainPage.xaml(.cs)             # メイン画面UIとロジック
│   └── STORE_PUBLISH_GUIDE.md         # Microsoft Store 申請メモ (識別情報はマスク済み)
├── EasyFileTransfer.Linux/            # Linux / クロスプラットフォーム版 (Avalonia UI / net10.0)
│   ├── Services/                      # XDG Base Directory 対応の設定永続化 (config.json)
│   ├── ViewModels/                    # MVVM ViewModel (CommunityToolkit.Mvvm)
│   ├── Views/                         # Avalonia ウィンドウ定義
│   └── README.md                      # Linux版の補足ドキュメント
├── build-msix-easyfiletransfer.ps1    # Windows MSIX パッケージ作成用 PowerShell スクリプト
├── build-msix-easyfiletransfer.cmd    # 上記スクリプトのランチャー
├── EasyFileTransfer.sln               # ソリューションファイル
└── EasyFileTransfer.slnx              # XML ソリューションファイル
```

---

## 開発環境・ビルド方法

### 必要な環境
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Windows版 (.NET MAUI) をビルドする場合: MAUI ワークロード (`dotnet workload install maui-windows`)

### Linux / クロスプラットフォーム版 (Avalonia UI) の実行

```bash
dotnet run --project EasyFileTransfer.Linux/EasyFileTransfer.Linux.csproj
```

単一バイナリとして発行する場合（`linux-x64` の例）：

```bash
dotnet publish EasyFileTransfer.Linux/EasyFileTransfer.Linux.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

詳細は [EasyFileTransfer.Linux/README.md](EasyFileTransfer.Linux/README.md) もあわせてご覧ください。

### Windows版 (.NET MAUI) のビルド

```bash
dotnet build EasyFileTransfer/EasyFileTransfer.csproj -f net10.0-windows10.0.19041.0
```

---

## 公開にあたっての補足（Microsoft Store 設定について）

本リポジトリ公開にあたり、作者個人の **Microsoft Store 公開用ID・パブリッシャー情報・署名用証明書 (`.pfx` / `.cer`)** は削除、または `XXXX` に置き換えています。  
手元で MSIX パッケージをビルド・署名したい場合は、以下のファイルを必要に応じてご自身の値に書き換え、署名用証明書をローカルに配置してください。

- `EasyFileTransfer/EasyFileTransfer.csproj`（`ApplicationId`）
- `EasyFileTransfer/Platforms/Windows/Package.appxmanifest`（`Identity` の `Name` / `Publisher`、`PublisherDisplayName`）
- `build-msix-easyfiletransfer.ps1` / `EasyFileTransfer/STORE_PUBLISH_GUIDE.md`

---

## 注意事項

- 使いやすさ重視でローカルネットワーク内（HTTP）での通信としています。カフェや公衆Wi-Fiなど信頼できないネットワーク上では使用しないでください。
- 初めての公開のため、コードや設計に改善点があるかと思います。気づいた点があれば Issue や Pull Request などで教えていただけると嬉しいです！
