# ToolVPS

A lightweight Windows desktop tool for managing VPS servers over SSH — no Vultr API key required to use the core features.

Built with WPF / .NET 8, MVVM pattern, SSH.NET.

---

## Features

### SSH Terminal
- Connect via **password** or **private key** (Ed25519 / RSA)
- Embedded terminal with live shell streaming
- ANSI output cleaned for readable display (`dumb` terminal mode — compatible with `codex`, `claude` CLI, etc.)
- Terminal `cd`s automatically when you open a folder in the file browser
- Connection info (host, port, user, key path) saved and restored on next launch

### Remote File Manager
- Browse the remote filesystem via SFTP (side panel)
- **Create folder** and **Create file** directly on the server
- **Open** files into the built-in editor
- **Edit and save** files back to the server
- Navigate up/down directories; file list scrolls when many entries

### SSH Key Management
- **Generate** RSA (4096-bit) or Ed25519 key pairs, saved to any local directory
- **Load** an existing private key and derive / display its public key
- **Install** a public key to a remote server (equivalent to `ssh-copy-id`)

### Docker Management
- List all containers with status, image, and ports
- **Start / Stop / Restart** containers
- Stream **container logs**
- List Docker Compose services and their port bindings
- **Port tunneling** shortcuts: one-click local tunnels for Postgres (5432), MySQL (3306), Redis (6379), or any custom port

### Vultr Integration *(optional)*
- If a Vultr API key is configured, lists all instances with IP, status, region, plan, OS, vCPU, RAM
- **Reboot** instances from the UI
- Click an instance to pre-fill SSH connection details

---

## Requirements

- Windows 10 / 11
- .NET 8 Desktop Runtime
- SSH access to your VPS (password or private key)

---

## Getting Started

1. Clone / download the repo
2. `dotnet run` or open `ToolVPS.sln` in Visual Studio and press F5
3. Go to the **Terminal** tab, enter your server's IP, port, username, and credential
4. Click **Connect**

No API key needed — all SSH features work standalone.

---

## Tech Stack

| Library | Purpose |
|---|---|
| [SSH.NET](https://github.com/sshnet/SSH.NET) | SSH / SFTP client |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MVVM source generators |
| BouncyCastle | Ed25519 key generation |
| Microsoft.Extensions.DependencyInjection | DI container |
