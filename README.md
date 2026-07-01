<!-- Common Project Tags:
desktop-app 
desktop-application 
dotnet 
netframework 
netframework48 
tool 
tools 
vbnet 
visualstudio 
windows 
windows-app 
windows-application 
windows-applications 
windows-forms 
winforms 
image 
viewer 
images 
image-viewer 
file-viewer 
image-visualizer 
windowsapp 
desktopapp 
image-view 
image-visualization 
image-caching 
desktopapplication 
windowsapplication 
 -->

<div align="center">
  <img src="https://github.com/ElektroStudios/Fast-Image-Viewer/blob/main/Images/App.ico" width="80" alt="FIV Logo">
  
  <h1>Fast Image Viewer (FIV)</h1>

### A minimalist, high-performance image viewer written purely in VB.NET and powered by a smart background-caching engine for lag-free navigation.

</div>

------------------

## 👋 Introduction

**FIV** is a responsive image viewer built with one primary goal: zero loading lag. At its core is a custom-built, multi-threaded Smart Image Cache system that completely eliminates the blocking waiting times usually associated with browsing heavy, high-resolution images. 

While you are looking at the current picture, **FIV**'s background engine is already silently reading the disk, pre-loading the upcoming files directly into memory. When you press the next or previous key, the image is instantly displayed from RAM. No bottlenecks, no disk stutter, just instant rendering.

## 🖼️ Screenshots

![FIV screenshot 1](/Images/screenshot1.png)

## 🎦 Videos

[FIV DEMO VIDEO](https://github.com/user-attachments/assets/2cf72b93-6007-4383-af50-a986469bbd1c)

## 👌 Features

* **Smart Asymmetric Caching:** The heart of **FIV**. A dedicated background worker thread pre-loads images ahead of and behind your current position. You configure the cache radius (e.g., 50 images forward, 5 backward), and the engine handles the memory management. Evicted images are automatically disposed of without blocking the UI.
* **1:1 True Size View:** Instantly toggle actual pixel size with a double-click and pan massive images smoothly via mouse drag or keyboard steps.
* **Rapid File Management:** Copy files, convert image formats, move them to custom directories, or delete them directly from the viewer using single keystrokes.
* **Unrestricted Long Path Support:** Native bypass for the Windows 260-character MAX_PATH limit. It handles extremely deep and complex folder structures flawlessly.
* **Keyboard-Driven & Responsive:** Built for power users. Navigate rapidly, rotate, and zoom with zero interface clutter getting in your way.

## ⌨️ Hotkeys & Navigation

FIV is designed to be fully controlled via keyboard for maximum comfort:

| Key / Input | Action |
| :--- | :--- |
| `Right Arrow` / `PageDown` | Next image |
| `Left Arrow` / `PageUp` / `Backspace` | Previous image |
| `Home` / `End` | Jump to First / Last image in directory |
| `+` / `-` | Zoom In / Zoom Out |
| `L` / `R` | Rotate image Left / Right |
| `Delete` | Send to Recycle Bin (or move to Custom Folder, depending on settings) |
| `Shift` + `Delete` | Permanently delete file from disk |
| `Escape` | Exit Fullscreen or Exit 1:1 True Size view |
| `Middle Click` | Toggle Fullscreen mode |
| `Double Click` | Toggle 1:1 True Size view |
| `Mouse Wheel` | Next / Previous image |
| `Arrows` (in 1:1 view) | Pan image Up / Down / Left / Right |
| `Left Click + Drag` (in 1:1 view) | Pan image freely |

## ⚠️ Limitations

* **Supported Formats:** **FIV** only opens the following extensions:
  * `.bmp`
  * `.jpg` / `.jpeg`
  * `.png`
  * `.tif` / `.tiff`
  * `.webp`

  Any other image format inside a directory is completely ignored by the viewer.

## 📝 Requirements

- Microsoft Windows OS (64-Bit).

## 🤖 Getting Started

Download the latest release by clicking [here](https://github.com/ElektroStudios/Fast-Image-Viewer/releases/latest) and start using it!.

## 🔄 Change Log

Explore the complete list of changes, bug fixes, and improvements across different releases by clicking [here](/Docs/CHANGELOG.md).

## 🏆 Credits

This work relies on the following resources: 

 - [.NET Framework](https://dotnet.microsoft.com/en-us/download/dotnet-framework)
 - [Ookii.Dialogs](https://www.ookii.org/software/dialogs/)
 - [Claude 4 Opus (Anthropic)](https://claude.ai/) - Assisted in writing code from scratch and refining core components.

## ⚠️ Disclaimer:

This Work (the repository and the content provided in) is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In no event shall the authors or copyright holders be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the Work or the use or other dealings in the Work.

This Work has no affiliation, approval or endorsement by the author(s) of the third-party libraries used by this Work.

## 💪 Contributing

Your contribution is highly appreciated!. If you have any ideas, suggestions, or encounter issues, feel free to open an issue by clicking [here](https://github.com/ElektroStudios/Fast-Image-Viewer/issues/new/choose). 

Your input helps make this Work better for everyone. Thank you for your support! 🚀

## 💰 Beyond Contribution 

This work is distributed for educational purposes and without any profit motive. However, if you find value in my efforts and wish to support and motivate my ongoing work, you may consider contributing financially through the following options:

<br></br>
<p align="center"><img src="/Images/github_circle.png" height=100></p>
<p align="center">__________________</p>
<h3 align="center">Becoming my sponsor on Github:</h3>
<p align="center">You can show me your support by clicking <a href="https://github.com/sponsors/ElektroStudios/">here</a>, <br align="center">contributing any amount you prefer, and unlocking rewards!</br></p>
<br></br>

<p align="center"><img src="/Images/paypal_circle.png" height=100></p>
<p align="center">__________________</p>
<h3 align="center">Making a Paypal Donation:</h3>
<p align="center">You can donate to me any amount you like via Paypal by clicking <a href="https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=E4RQEV6YF5NZY">here</a>.</p>
<br></br>

<p align="center"><img src="/Images/envato_circle.png" height=100></p>
<p align="center">__________________</p>
<h3 align="center">Purchasing software of mine at Envato's Codecanyon marketplace:</h3>
<p align="center">If you are a .NET developer, you may want to explore '<b>DevCase Class Library for .NET</b>', <br align="center">a huge set of APIs that I have on sale. Check out the product by clicking <a href="https://codecanyon.net/item/elektrokit-class-library-for-net/19260282">here</a></br><br align="center"><i>It also contains all piece of reusable code that you can find across the source code of my open source works.</i></p>
<br></br>

<h2 align="center"><u>Your support means the world to me! Thank you for considering it!</u> 👍</h2>
