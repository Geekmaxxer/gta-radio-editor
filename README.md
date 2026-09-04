# GTA 5 Port Radio Editor

GTA 5 Port Radio Editor is a Windows desktop app for replacing existing (ported) GTA V Legacy radio songs without hand-authoring a CSV, converting tracks manually, or importing dozens of `.oac` files one at a time.

It reads a radio `.rpf`, finds the actual music containers, lets the user link MP3/WAV files to slots by drag-and-drop, converts each assignment to GTA-compatible audio, and builds a **new** RPF. The source archive is never modified.

## What it does

- Starts from a selected GTA V game or port directory and recursively discovers `RADIO_*.rpf` archives, without assuming an `x64`, `switch`, or fixed `sfx` folder path.
- Lists discovered archives in a station dropdown using familiar station names, such as West Coast Classics and Radio Los Santos, rather than only archive codes.
- Opens the selected station and detects its stereo music AWC containers.
- Excludes short station IDs and imaging clips, so the West Coast Classics source archive reports its 29 music slots.
- Loads `.mp3`, `.wav`, `.flac`, `.aac`, `.m4a`, `.wma`, and Ogg Vorbis `.ogg` files from one or more music folders. The native folder picker supports Ctrl/Shift multi-selection, so several artist folders can be added in one action.
- Assigns a track to any radio slot through drag-and-drop, double-click assignment, or ordered auto-fill.
- Converts each assignment to two 48 kHz, 16-bit PCM mono channels while retaining the original stereo image.
- Rebuilds the matching AWC data while preserving the original container metadata and radio events.
- Validates every rebuilt AWC before placing it in a new output RPF.

## Requirements

- Windows 10/11 x64.
- A GTA V directory containing RPF archives compatible with CodeWalker. The station finder supports varied folder layouts, but encrypted archives still require compatible archive keys.
- MP3, WAV, FLAC, AAC, M4A, WMA, or Ogg Vorbis input. AAC/M4A/WMA/FLAC decoding uses Windows Media Foundation; Windows N installations may need the Media Feature Pack.
- A mods-folder loader such as OpenIV.asi to make GTA V use the finished RPF. This is 100% optional for most ports, only GTA 5 Legacy PC needs OpenIV.asi.
- A normal GTA 5 Legacy PC install won't let you load it's tables/rpf files unless patched by a port or OpenIV.asi is installed.

## Use

1. Launch `GTARadioEditor.exe`.
2. Choose the GTA V game or port directory. The app finds `RADIO_*.rpf` files anywhere under it.
3. Select a friendly station name from the **Radio station** dropdown, then click **Open selected station**. The table lists only real music slots.
4. Click **Add folders**, Ctrl/Shift-select one or more music folders, and confirm once.
5. Drag a music entry from the right-hand list onto a radio slot, or select both and use **Assign selected**. `Auto-fill in order` is available when a deliberate order is not important.
6. Click **Build output RPF** and select a new folder. Keep the prefilled original RPF file name unchanged; the application will refuse to overwrite the selected source archive or rebuild under a different name.
7. Put the output in the equivalent path under GTA V's architecture folder. For example:

   `\x64\audio\sfx\RADIO_09_HIPHOP_OLD.rpf` or
   `\switch\audio\sfx\RADIO_09_HIPHOP_OLD.rpf`

   Do not use modified audio in GTA Online.

## Build from source

Clone with submodules so the bundled CodeWalker Core project is present:

```powershell
git clone --recurse-submodules https://github.com/Geekmaxxer/gta-radio-editor
cd gta-radio-editor\GTARadioEditor
dotnet restore .\GTARadioEditor\GTARadioEditor.csproj
dotnet msbuild GTARadioEditor\GTARadioEditor.csproj `
  -target:BuildNet48SingleExe `
  -property:Configuration=Release `
  -property:TargetFramework=net48
```

The self-contained Windows build is written to:

`GTARadioEditor\bin\Release\net48`

`GTARadioEditor.exe` is self-contained. The adjacent `.pdb` file, if emitted, is only for debugging and is not needed to run the app.

For a read-only diagnostic of a radio RPF from source, run:

```powershell
dotnet run -c Release -- --scan "C:\Path\To\RADIO_09_HIPHOP_OLD.rpf"
```

## Update checking

On startup, the app silently checks
[`/releases/latest`](https://github.com/Geekmaxxer/gta-radio-editor/releases/latest)
on GitHub and compares its tag to the running app's version (shown in small
text in the status bar, and in the title bar). That endpoint always
resolves to the newest published, non-draft, non-prerelease release, so a
beta build with a higher-looking version number - like the earlier
`v0.1-beta`/`v0.2-beta`/`v0.3-beta` tags - is never mistaken for the latest
stable release.

If a newer version is found, a small dialog offers **Take me there** (opens
the release page in your browser) or **I'm good** (dismisses it for the
rest of that session). There's no persisted "don't ask again" - closing and
reopening the app checks again.

If you're maintaining this repo: bump `AppVersion.Current` in
`AppVersion.cs` (and `<Version>` in the `.csproj`) every time you cut a new
version, and make sure it's published as an actual GitHub **Release** with
the "pre-release" checkbox left unchecked - a bare git tag, or a release
still flagged as pre-release, won't be picked up by `/releases/latest`. If
the check fails for any reason (offline, rate-limited, no stable release
published yet), it fails silently and the app works normally.

## Notes on compatibility

- The station finder is path-layout independent: it works whether a compatible archive is stored below `x64`, `switch`, or another port-specific directory. Archive parsing/rebuilding remains limited to formats and encryption keys supported by CodeWalker.
- The app builds a direct AWC/RPF result internally; it does not create temporary OpenIV `.oac` files because that intermediate step is no longer necessary.
- The output RPF is a new file. This provides a simple rollback path: create a backup of the original RPF file to make sure you can rollback safely.

## Credits

- [CodeWalker Core](https://github.com/dexyfex/CodeWalker) is included as a git submodule for RPF/AWC parsing and serialization. Its notices are in `vendor/CodeWalker/Notice.txt`.
- [NAudio](https://github.com/naudio/NAudio) is used for input audio decoding and resampling.
