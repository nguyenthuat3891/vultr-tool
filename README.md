# ToolVPS

A Windows desktop tool for managing VPS servers over SSH. Works fully without a Vultr API key — the API key is optional and only needed to list Vultr instances.

Built with WPF / .NET 8, MVVM (CommunityToolkit), SSH.NET.

---

## Tabs

### Instances
- **Vultr API key** field (masked) — paste your key, click **Save & Apply** to load instances. Key is saved and restored on next launch.
- Lists all Vultr instances: Label, IP, Status, Region, Plan, OS, vCPU, RAM, Created date
- **Refresh** / **Reboot** selected instance
- **Open Local SSH Window** — launches Windows Terminal or CMD with `ssh user@ip`
- **Quick SSH Connect** panel — enter Host, Port, User, Password / private key path and click **SSH Connect** to open the embedded terminal directly. All fields (except password) are saved and restored on next launch.

### Terminal (SSH)
- Connect via **Password** or **Private Key** (Ed25519 / RSA)
- Host, port, username, and key path are saved and restored on next launch
- Embedded terminal with live shell output — ANSI escape codes stripped for clean display (`dumb` terminal mode, compatible with tools like `codex`, `claude` CLI)
- `PAGER=cat` set automatically so `git log`, `man`, etc. do not block
- Type commands in the input bar and press **Enter** or **Send**
- **Disconnect** button ends the session

#### Remote File Manager (right panel)
- Browse the remote filesystem via SFTP
- **Up** button navigates to parent directory
- **Double-click** a folder to enter it — terminal also `cd`s to that path automatically
- **Create folder / Create file** — type a name in the input box and click `+ Folder` or `+ File`
- **Open** a file to load it into the built-in editor
- **Save File** writes edits back to the server
- File list is fixed height (scrollable); editor expands with the window

### SSH Keys
- **Generate** RSA (4096-bit) or Ed25519 key pairs — choose name, save directory, and key type
- **Load** an existing private key — RSA public key is derived automatically; Ed25519 reads the `.pub` file
- **Install Public Key** to a remote server (equivalent to `ssh-copy-id`) — connects once with password to append the key to `~/.ssh/authorized_keys` with correct permissions

### Docker
- **Check Docker** — verifies Docker is installed and returns version
- **Refresh All** — lists all containers and Docker Compose services
- Start / **Stop / Restart** selected containers
- **View Logs** — streams recent container output
- Lists Compose services with port bindings
- **Port tunneling** — one-click local SSH tunnels:
  - Postgres → `127.0.0.1:5432`
  - MySQL → `127.0.0.1:3306`
  - Redis → `127.0.0.1:6379`
  - Custom port — enter local and remote port manually
- Active tunnels shown with **Remove** button

---

## Settings Persistence

All settings are stored in `%APPDATA%\ToolVPS\settings.json`.

| Field | Saved when |
|---|---|
| Vultr API key | "Save & Apply" clicked |
| Quick SSH — Host, Port, User, Key path | "SSH Connect" clicked |
| Terminal — Host, Port, User, Auth mode, Key path | Successful SSH connect |

Passwords are never saved.

---

## Requirements

- Windows 10 / 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- SSH access to your VPS

---

## Getting Started

1. Clone or download the repo
2. Open `ToolVPS.sln` in Visual Studio and press **F5**, or run `dotnet run` from the `ToolVPS/` folder
3. Go to the **Instances** tab → **Quick SSH Connect** — enter your server IP and credentials → **SSH Connect**

No Vultr API key required for any SSH feature.

---

## Tech Stack

| Library | Version | Purpose |
|---|---|---|
| [SSH.NET](https://github.com/sshnet/SSH.NET) | 2024.2.0 | SSH shell, SFTP, port forwarding |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | 8.3.2 | MVVM source generators, RelayCommand |
| [BouncyCastle.Cryptography](https://github.com/bcgit/bc-csharp) | 2.4.0 | Ed25519 key pair generation |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | DI container |
| Microsoft.Extensions.Http | 8.0.1 | HttpClient factory for Vultr API |
