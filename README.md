<h1 align="center">🎥 TubeFetchr</h1>
<p align="center">
  <b>A lightweight YouTube video & audio downloader built with C# (WinForms) and YoutubeExplode</b><br>
  <i>Download videos, extract MP3, choose quality, and enjoy!</i>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-blue.svg" />
  <img src="https://img.shields.io/badge/C%23-WinForms-green.svg" />
  <img src="https://img.shields.io/github/license/wwmanidumaneesha/tubefetchr" />
</p>

---

## ✨ Features

- 🎯 Fetch video title, thumbnail, and available qualities
- 📥 Download video in selected resolution or audio-only (MP3)
- 💾 Muxed & separate video/audio merging with `ffmpeg`
- 🍪 Load YouTube cookies from `cookies.txt` for restricted/private content
- 🎨 Clean and modern UI with progress bar and status messages

---

## 🖼️ Preview

> Simple, intuitive layout for ease of use!

<p align="center">
  <img src="ui.png" alt="TubeFetchr UI Screenshot" width="800" />
</p>

---

## 🚀 Getting Started

### 🔧 Requirements

- Windows 10 or newer
- [.NET 8.0 Runtime or SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Visual Studio 2022 or newer

---

### 🧑‍💻 How to Use

1. Clone this repository:

   ```bash
   git clone https://github.com/wwmanidumaneesha/tubefetchr.git
   ```

2. Open the solution `TubeFetchr.sln` in Visual Studio.

3. Build and Run the app.

4. Paste a YouTube video URL and click **🎯 Fetch Info**.

5. Choose your desired quality and click **⬇ Download**.

---

## 🍪 Cookie Support

- For age-restricted/unlisted/private videos, use cookies exported from your browser.
- Use [Get cookies.txt extension](https://chrome.google.com/webstore/detail/get-cookiestxt/ojkcdipcgfaekbeaelaapakgnjflfglf) to export them.
- Load the `cookies.txt` file using the **🍪 Load Cookies** button.

---

## 📦 Project Structure

```
📦 TubeFetchr/
 ┣ 📂Assets/
 ┃ ┗ 📄 TubeFetchr.ico
 ┣ 📂Resources/
 ┃ ┗ 📄 ffmpeg.exe
 ┣ 📄 MainForm.cs
 ┣ 📄 MainForm.Designer.cs
 ┣ 📄 Program.cs
 ┣ 📄 FodyWeavers.xml
 ┣ 📄 TubeFetchr.csproj
 ┣ 📄 TubeFetchr.sln
 ┣ 📄 .gitignore
 ┣ 📄 README.md
 ┗ 📄 ui.png
```

---

## 🔧 Tech Stack

- **Frontend:** Windows Forms (.NET 8)
- **Backend:** [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode)
- **Media Tool:** Embedded `ffmpeg.exe`

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

## 👨‍💻 Author

**Manidu Maneesha**  
GitHub: [@wwmanidumaneesha](https://github.com/wwmanidumaneesha)  
Email: manidumaneeshaww@gmail.com  

---

## 🌟 Support

If you find this tool useful, please consider giving it a ⭐ on GitHub.  
Your feedback and support help improve the project!