# EasyFileTransfer（簡単ファイル転送）

[![Releases](https://img.shields.io/github/v/release/kumatarosasaki3891-art/EasyFileTransfer?label=Linux%20Release&color=0284c7)](https://github.com/kumatarosasaki3891-art/EasyFileTransfer/releases)
[![Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Windows%2010%2F11-f97316)](https://apps.microsoft.com/detail/9N4G1LLL2VZP?hl=ja&gl=JP&ocid=pdpshare)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

スマホとPCの間で、QRコードを読み取るだけでファイルを双方向にやり取りできるローカルファイル転送ツールです。  
外部サーバーやインターネット回線を経由せず、同じ Wi-Fi（LAN）内だけで直接通信する仕組みになっています。

> A fast, local-first file transfer tool between your smartphone and PC (Windows / Linux) via QR code—no cloud, no account, and no mobile app required.

---

## 📥 ダウンロード / Download

- **🐧 Linux版 (AppImage / バイナリ)**: [GitHub Releases からダウンロード](https://github.com/kumatarosasaki3891-art/EasyFileTransfer/releases/latest)
- **🪟 Windows 10 / 11版**: [Microsoft Store から無料で入手](https://apps.microsoft.com/detail/9N4G1LLL2VZP?hl=ja&gl=JP&ocid=pdpshare)

---

## 📸 画面イメージ / Screenshots

<!-- ↓ GitHubの編集画面にスクリーンショット画像やデモ動画(.webm)をドラッグ＆ドロップして貼り付けてください -->
| PC側メイン画面（受信・QR表示） | スマホ側ブラウザ画面（アプリ不要） |
| :---: | :---: |
| <img width="540" alt="easy_file_transfer_top" src="https://github.com/user-attachments/assets/cef76133-bc57-4d38-a136-3ec70bf3acaa" /> | <img width="240" alt="easy_file_transfer_send" src="https://github.com/user-attachments/assets/7d16bc33-5c0c-48b7-8232-b49f0eb5169e" /> |

---

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
2. PC でアプリを起動します（Linux版バイナリの場合は `chmod +x` で実行権限を付与して起動）。
3. **スマホ → PC に送りたいとき**
   - 「受信 (スマホ → PC)」タブを開き、スマホの標準カメラでQRコードをスキャンします。
   - 開いたWeb画面で写真やファイルを選んで送信すると、PCの保存先フォルダー（初期値: `Downloads/EasyFileTransfer`）へ保存されます。
4. **PC → スマホ に送りたいとき**
   - 「送信 (PC → スマホ)」タブを開き、「＋ ファイルを追加」ボタン（または共有フォルダー `Downloads/EasyFileTransfer/Shared` への直接配置）でファイルを用意します。
   - スマホでQRコードをスキャンして、表示された一覧からダウンロードします。

> **つながらないときのヒント**
> - PCに仮想ネットワーク（WSL / Docker / Hyper-V / VPN など）があると別IPが選ばれることがあるため、画面の「🔄 IP切替」ボタンを試してみてください。
> - ルーターの「プライバシーセパレーター（AP分離）」が有効になっていると端末同士が通信できないため、無効化されているかご確認ください。
> - ファイアウォール（Windows Defender / `ufw` 等）のブロックが出た場合は、ローカルネットワークでの通信ポートを許可してください。

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
