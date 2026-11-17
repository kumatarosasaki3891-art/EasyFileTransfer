# EasyFileTransfer

[![Releases](https://img.shields.io/github/v/release/kumatarosasaki3891-art/EasyFileTransfer?label=Linux%20Release&color=0284c7)](https://github.com/kumatarosasaki3891-art/EasyFileTransfer/releases)
[![Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Windows%2010%2F11-f97316)](https://apps.microsoft.com/detail/9N4G1LLL2VZP?hl=ja&gl=JP&ocid=pdpshare)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

English | [日本語](README.md)

A local file transfer tool that lets you exchange files in both directions between your smartphone and PC simply by scanning a QR code.  
It communicates directly within the same Wi-Fi (LAN) network, without going through any external server or internet connection.

> A fast, local-first file transfer tool between your smartphone and PC (Windows / Linux) via QR code—no cloud, no account, and no mobile app required.

---

## 📥 Download

- **🐧 Linux (Flatpak bundle)**: [Download from GitHub Releases](https://github.com/kumatarosasaki3891-art/EasyFileTransfer/releases/latest)
- **🪟 Windows 10 / 11**: [Get it free from the Microsoft Store](https://apps.microsoft.com/detail/9N4G1LLL2VZP?hl=us&gl=US&ocid=pdpshare)

---

## 📸 Screenshots

<!-- ↓ Drag and drop screenshot images or demo videos (.webm) here in the GitHub editor to attach them -->
| PC main screen (receive / QR code display) | Smartphone browser screen (no app required) |
| :---: | :---: |
| <img width="540" alt="easy_file_transfer_top" src="https://github.com/user-attachments/assets/cef76133-bc57-4d38-a136-3ec70bf3acaa" /> | <img width="240" alt="easy_file_transfer_send" src="https://github.com/user-attachments/assets/7d16bc33-5c0c-48b7-8232-b49f0eb5169e" /> |

---

> This is my first attempt at publishing something as open source.  
> The code was written while I was still learning, so some parts may be a little rough, but I hope it will be useful as a reference for anyone who would like to build a similar tool!

---

## Key Features

- **No dedicated app required on the smartphone**
  - Scan the QR code shown on the PC with your smartphone's camera, and a web page for sending and receiving files opens in the browser.
- **Bidirectional file transfer**
  - **Receive mode (smartphone → PC)**: Send files to the PC from your smartphone by selecting files, taking a photo with the camera, or drag & drop (up to 2 GB per file). If a file with the same name already exists, it is automatically saved under a different name with `(1)`, `(2)`, and so on.
  - **Send mode (PC → smartphone)**: Files placed in the PC's shared folder can be listed and downloaded from the smartphone's browser.
- **Lightweight HTTP server that needs no administrator privileges (`FileTransferServer.cs`)**
  - Because it is implemented on top of `TcpListener` rather than `HttpListener`, it can start on Windows without UAC administrator privileges or complicated `netsh` configuration (default port `5050`; if that port is in use, it automatically looks for a free one).
- **One-to-one disposable session protection**
  - A random 8-digit session token (valid for 30 minutes) is issued and tied to the IP address of the first device that connects, blocking access from other devices on the same network.
- **In-house QR code generation (`QrCodeGenerator.cs`)**
  - Wanting to avoid adding an external QR generation library, I implemented QR code rendering (ISO/IEC 18004, Versions 1–7) using `SkiaSharp`.
- **Support for 11 languages (`LocalizationService.cs`)**
  - Both the PC UI and the smartphone web page can be switched between 11 languages: Japanese, English, Simplified Chinese, Korean, Traditional Chinese, Vietnamese, Spanish, German, French, Portuguese, and Italian.
- **Windows version (.NET MAUI) & Linux version (Avalonia UI)**
  - The models and the server / QR / localization logic are shared, with only the UI built separately for each platform.

---

## How to Use

1. Connect the PC and the smartphone to the **same Wi-Fi (the same network)**.
2. Start the app on the PC (for the Linux version, install the Flatpak bundle and then launch it).
3. **To send from the smartphone → PC**
   - Open the "Receive (Smartphone → PC)" tab and scan the QR code with the smartphone's standard camera.
   - On the web page that opens, select photos or files and send them; they are saved to the destination folder on the PC (default: `Downloads/EasyFileTransfer`).
4. **To send from the PC → smartphone**
   - Open the "Send (PC → Smartphone)" tab and prepare the files using the "+ Add File" button (or by placing them directly in the shared folder, `Downloads/EasyFileTransfer/Shared`).
   - Scan the QR code with the smartphone and download the files from the list that appears.

> **Tips if you can't connect**
> - If the PC has virtual networks (WSL / Docker / Hyper-V / VPN, etc.), a different IP address may be selected, so try the "🔄 Switch IP" button on the screen.
> - If your router's "privacy separator (AP isolation)" is enabled, devices cannot communicate with each other, so please check that it is disabled.
> - If a firewall (Windows Defender, `ufw`, etc.) blocks the connection, please allow the communication port on your local network.

---

## Directory Structure

```text
EasyFileTransfer/
├── EasyFileTransfer/                  # Windows app (.NET MAUI / net10.0-windows10.0.19041.0)
│   ├── Models/                        # Models for received / shared file information (shared with the Linux version)
│   ├── Services/                      # HTTP server, QR generation, localization, folder settings
│   ├── Platforms/                     # Platform-specific entry points / Package.appxmanifest
│   ├── MainPage.xaml(.cs)             # Main screen UI and logic
│   └── STORE_PUBLISH_GUIDE.md         # Microsoft Store submission notes (identifying information masked)
├── EasyFileTransfer.Linux/            # Linux / cross-platform version (Avalonia UI / net10.0)
│   ├── Services/                      # Settings persistence compatible with the XDG Base Directory specification (config.json)
│   ├── ViewModels/                    # MVVM ViewModels (CommunityToolkit.Mvvm)
│   ├── Views/                         # Avalonia window definitions
│   └── README.md                      # Supplementary documentation for the Linux version
├── build-msix-easyfiletransfer.ps1    # PowerShell script for creating the Windows MSIX package
├── build-msix-easyfiletransfer.cmd    # Launcher for the above script
├── EasyFileTransfer.sln               # Solution file
└── EasyFileTransfer.slnx              # XML solution file
```
