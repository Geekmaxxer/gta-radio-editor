# GTA 5 Port Radio Editor ; v0.6 - Stable

GTA 5 Port Radio Editor is a Windows desktop app for replacing existing (ported) GTA V Legacy radio songs without hand-authoring a CSV, converting tracks manually, or importing dozens of `.oac` files one at a time.

It reads a radio `.rpf`, finds the actual music containers, lets the user link music files to slots by drag-and-drop, and converts each assignment to GTA-compatible audio. It builds **new** radio and timing outputs so the replacement audio and radio schedule use the same song length. Source files are never modified.

## What it does

- Offers English, Spanish, Russian, Simplified Chinese, Portuguese and German in the **Language** selector. The choice is remembered, and changing it updates the interface without clearing loaded music or assignments.
- Stores all six translations in `GTARadioEditor/Locales/*.json`, including the English catalog. These files are embedded in the EXE, so no translation files need to be installed separately.
- Starts from a selected GTA V game or port directory and recursively discovers `RADIO_*.rpf` archives, without assuming an `x64`, `switch`, or fixed `sfx` folder path.
- Lists discovered archives in a station dropdown using familiar station names, such as West Coast Classics and Radio Los Santos, rather than only archive codes.
- Opens the selected station and detects its stereo music AWC containers.
- Excludes short station IDs and imaging clips, so the West Coast Classics source archive reports its 29 music slots.
- Loads `.mp3`, `.wav`, `.flac`, `.aac`, `.m4a`, `.wma`, and Ogg Vorbis `.ogg` files from one or more music folders. The native folder picker supports Ctrl/Shift multi-selection, so several artist folders can be added in one action.
- Assigns a track to any radio slot through drag-and-drop, double-click assignment, or ordered auto-fill.
- **Random Auto-fill** shuffles loaded tracks and assigns them without repeating tracks within a fill. Both auto-fill actions replace the current assignment list; slots beyond the number of loaded tracks keep their original GTA audio.
- **Clear All Replaced** removes every replacement assignment while keeping the loaded music and original game files.
- Converts each assignment to two 48 kHz, 16-bit PCM mono channels while retaining the original stereo image.
- Calculates each song's duration from the actual converted sample count, including resampling and decoder output.
- Matches the station/container and left/right waveform hashes to streaming sounds in `sounds.dat54.rel` or the DLC's `*_sounds.dat54.rel`, then patches their duration in milliseconds.
- Moves the AWC's sample-based cues proportionally to the replacement length while retaining cue names and values, including DJ and transition events.
- Validates the serialized audio, cue bounds and timing metadata, then reopens both output archives to verify their packaged contents.
- Stages both outputs before replacing earlier builds. Failed conversions and cancelled builds preserve existing outputs.

## Requirements

- Windows 10/11 x64.
- A GTA V directory containing RPF archives compatible with CodeWalker. The station finder supports varied folder layouts, but encrypted archives still require compatible archive keys.
- The matching radio timing metadata: normally `audio_rel.rpf` for base stations, or the archive containing the DLC station's `*_sounds.dat54.rel`. An extracted `.dat54.rel` is also supported.
- MP3, WAV, FLAC, AAC, M4A, WMA, or Ogg Vorbis input. AAC/M4A/WMA/FLAC decoding uses Windows Media Foundation; Windows N installations may need the Media Feature Pack.
- A mods-folder loader such as OpenIV.asi to make GTA V use the finished RPF. This is 100% optional for most ports, only GTA 5 Legacy PC needs OpenIV.asi.
- A normal GTA 5 Legacy PC install won't let you load it's tables/rpf files unless patched by a port or OpenIV.asi is installed.

## Use

1. Launch `GTARadioEditor.exe`.
   Choose your preferred language from **Language**. The setting is saved in `%LOCALAPPDATA%\GTARadioEditor\settings.json`. Windows' own dialog buttons follow the Windows language.
2. Choose the GTA V game or port directory. The app finds `RADIO_*.rpf` files anywhere under it.
3. Select a friendly station name from the **Radio station** dropdown, then click **Open selected station**. The table lists only real music slots.
   Check **Radio timing file**. The app selects the nearby `audio_rel.rpf` when present. Use **Browse...** to select a different timing RPF or extracted `.dat54.rel`, especially for DLC stations. Missing matches block the build with an explanation.
4. Click **Add folders**, Ctrl/Shift-select one or more music folders, and confirm once.
5. Drag a music entry from the right-hand list onto a radio slot, or select both and use **Assign selected**. `Auto-fill in order` is available when a deliberate order is not important.
   Use **Random Auto-fill** to shuffle assignments, or **Clear All Replaced** to reset all replacement selections.
6. Click **Build output RPF** and select a new folder. Keep the prefilled original RPF file name unchanged. Both outputs are saved in this folder, using their original names. The app refuses to overwrite source files.
7. Install **both outputs** at the paths shown when the build finishes. For a base Switch station, for example:

   - Radio audio: `\switch\audio\sfx\RADIO_09_HIPHOP_OLD.rpf`
   - Radio timing: `\switch\audio\audio_rel.rpf`

   PC uses the equivalent `x64` paths under its mods setup. DLC outputs must replace their own original archives. If using an extracted REL, import it into its original entry in the game's timing archive; a loose REL beside the radio RPF is not loaded automatically. Back up the original archives before installing.

   When building several stations, select the **previously generated timing output** as the next station's timing input and use a new output folder. This carries forward earlier duration edits. Only the final combined timing archive needs to be installed, along with each rebuilt station RPF.

   Do not use modified audio in GTA Online.

## Build from source

Clone with submodules so the bundled CodeWalker Core project is present:

```powershell
git clone --recurse-submodules https://github.com/Geekmaxxer/gta-radio-editor
cd gta-radio-editor
dotnet restore .\GTARadioEditor\GTARadioEditor.csproj
dotnet msbuild GTARadioEditor\GTARadioEditor.csproj `
  -target:BuildNet48SingleExe `
  -property:Configuration=Release `
  -property:TargetFramework=net48
```

The single-EXE .NET Framework build is written to:

`GTARadioEditor\bin\Release\net48`

`GTARadioEditor.exe` embeds its managed dependencies and uses .NET Framework 4.8, included in current Windows 10/11 installations. Building it requires the .NET SDK; users do not need to install the modern .NET runtime. The retained `net10.0-windows` target supports a self-contained Windows publish.

For a read-only diagnostic of a radio RPF from source, run:

```powershell
dotnet run --project GTARadioEditor -c Release -f net10.0-windows -- --scan "C:\Path\To\RADIO_09_HIPHOP_OLD.rpf"
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

- CodeWalker's `Dat151RadioTrackSettings` contains sound/category/history references and a start offset; it has no song `Duration`, `IntroLength` or `OutroLength` field. In the tested Switch assets, the explicit song duration is `Dat54StreamingSound.Duration` in `sounds.dat54.rel`, while DJ/event cues are sample offsets in the AWC. This release patches those fields rather than inventing properties in `game.dat151.rel`. Existing dat151 records are preserved byte for byte.
- Cue positions are scaled from the vanilla song. They are not detected from the replacement's musical structure; DJ banter remains enabled and its content still refers to the original song. Beat/rockout cues also remain approximate for replacement music.
- Shared streaming definitions that also reference unrelated audio, unresolved references and unsupported cue layouts block the build. Support for separate tracks does not imply support for continuous DJ mixes.
- The station finder is path-layout independent: it works whether a compatible archive is stored below `x64`, `switch`, or another port-specific directory. Archive parsing/rebuilding remains limited to formats and encryption keys supported by CodeWalker.
- The app builds a direct AWC/RPF result internally; it does not create temporary OpenIV `.oac` files because that intermediate step is no longer necessary.
- The output RPF is a new file. This provides a simple rollback path: create a backup of the original RPF file to make sure you can rollback safely.

## Validation

Run generated-fixture checks without game files:

```powershell
dotnet run --project validation/GTARadioEditor.Validation -c Release -f net48
```

Run a complete build/reopen/decode check against your own compatible station and timing archives:

```powershell
dotnet run --project validation/GTARadioEditor.Validation -c Release -f net48 -- --integration `
  "C:\Path\To\Game\switch\audio\sfx\RADIO_09_HIPHOP_OLD.rpf" `
  "C:\Path\To\Game\switch\audio\audio_rel.rpf"
```

The validation generates distinct stereo tones, tests longer and shorter replacements, verifies cue positions and exact duration-only metadata edits, compares untouched assets and source hashes, and checks failed-build cleanup. Temporary outputs are removed. These checks establish file correctness; full playback and station transitions still need a test on the target console/port.

## Credits

- [CodeWalker Core](https://github.com/dexyfex/CodeWalker) is included as a git submodule for RPF/AWC parsing and serialization. Its notices are in `vendor/CodeWalker/Notice.txt`.
- [NAudio](https://github.com/naudio/NAudio) is used for input audio decoding and resampling.
