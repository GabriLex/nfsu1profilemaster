# NFSU1 Profile Master 🏁

A lightweight, user-friendly C sharp utility designed to edit savegames in **Need for Speed Underground 1**. It handles the technical heavy lifting like finding hidden save paths and bypassing Windows folder permissions.

## 🖼️ Screenshots

| Italian Interface | English Interface |
| :---: | :---: |
| ![Italian UI](patcher_it.png) | ![English UI](patcher.png) |

## ✨ Features

* **Auto-Detection**: Automatically finds save games in `%LOCALAPPDATA%`, `VirtualStore`, and common installation paths (including Steam versions).
* **Safety First**: Built-in "Extract" and "Restore" system to create backups before any modification.
* **Admin Privileges**: Automatically requests UAC elevation to ensure it can write save data in protected directories.
* **Bilingual**: Full support for both **Italian** and **English**.
* **Deep Scan**: Recursively searches for profile-specific files (save files with no extension) across multiple drives.
* **Rename Option**
* **UNLOCK PARTS OPTION**
* **MANUAL SAVEGAMES LOAD**
* **Player name and money reading**

Thanks MIFT0 for the patches
( i have reverse eng his program with Claude AI)

## 📦 Compiling to .exe

dotnet publish -c Release -r win-x64 --self-contained false -o publish


## License
GPL 3 
