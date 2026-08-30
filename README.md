# GTA Radio Editor

GTA Radio Editor is a Windows desktop app for replacing existing GTA V Legacy radio songs without hand-authoring a CSV, converting tracks manually, or importing dozens of `.oac` files one at a time.

It reads a radio `.rpf`, finds the actual music containers, lets the user link MP3/WAV files to slots by drag-and-drop, converts each assignment to GTA-compatible audio, and builds a **new** RPF. The source archive is never modified.

## What it does

- Starts from a selected GTA V game or port directory and recursively discovers `RADIO_*.rpf` archives, without assuming an `x64`, `switch`, or fixed `sfx` folder path.
- Lists discovered archives in a station dropdown using familiar station names, such as West Coast Classics and Radio Los Santos, rather than only archive codes.
- Opens the selected station and detects its stereo music AWC containers.
- Excludes short station IDs and imaging clips, so the West Coast Classics source archive reports its 29 music slots.
- Loads MP3/WAV files from one or more music folders. The native folder picker supports Ctrl/Shift multi-selection, so several artist folders can be added in one action.
- Assigns a track to any radio slot through drag-and-drop, double-click assignment, or ordered auto-fill.
- Converts each assignment to two 48 kHz, 16-bit PCM mono channels while retaining the original stereo image.
- Rebuilds the matching AWC data while preserving the original container metadata and radio events.
- Validates every rebuilt AWC before placing it in a new output RPF.

## Requirements

- Windows 10/11 x64.
- A GTA V directory containing RPF archives compatible with CodeWalker. The station finder supports varied folder layouts, but encrypted archives still require compatible archive keys.
- MP3 or PCM WAV input. MP3 decoding uses the Windows media components available on a normal GTA V Windows installation.
- A mods-folder loader such as OpenIV.asi to make GTA V use the finished RPF. (This is optional for the most part on ports)

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
git clone --recurse-submodules <your-repository-url>
cd gta-radio-editor\GTARadioEditor
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The self-contained Windows build is written to:

`GTARadioEditor\bin\Release\net10.0-windows\win-x64\publish\`

`GTARadioEditor.exe` is self-contained. The adjacent `.pdb` file, if emitted, is only for debugging and is not needed to run the app.

For a read-only diagnostic of a radio RPF from source, run:

```powershell
dotnet run -c Release -- --scan "C:\Path\To\RADIO_09_HIPHOP_OLD.rpf"
```

## Notes on compatibility

- The station finder is path-layout independent: it works whether a compatible archive is stored below `x64`, `switch`, or another port-specific directory. Archive parsing/rebuilding remains limited to formats and encryption keys supported by CodeWalker.
- The app builds a direct AWC/RPF result internally; it does not create temporary OpenIV `.oac` files because that intermediate step is no longer necessary.
- The output RPF is a new file. This provides a simple rollback path: create a backup of the original RPF file to make sure you can rollback safely.

## Credits

- [CodeWalker Core](https://github.com/dexyfex/CodeWalker) is included as a git submodule for RPF/AWC parsing and serialization. Its notices are in `vendor/CodeWalker/Notice.txt`.
- [NAudio](https://github.com/naudio/NAudio) is used for input audio decoding and resampling.
