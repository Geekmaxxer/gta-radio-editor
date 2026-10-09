# v0.6 validation

Validated on October 9, 2026 against the local Switch port's `RADIO_09_HIPHOP_OLD.rpf` and `audio_rel.rpf`.

- Both `net48` and `net10.0-windows` compiled successfully with zero warnings/errors. The .NET Framework single-EXE build and modern self-contained publish completed.
- Both framework versions passed the complete archive integration check. All 29 West Coast Classics music slots resolved to matching streaming duration records.
- Generated stereo audio replaced two real songs: one longer than its original and one shorter. The packaged audio decoded completely with distinct left/right channels and the expected 48 kHz sample counts.
- Packaged marker names/values were preserved; sample offsets matched the proportional duration changes.
- Exactly two streaming durations changed. REL bytes outside those four-byte fields, other metadata including dat151, unassigned songs and source-file SHA-256 hashes were unchanged.
- An empty replacement and a cancelled build preserved earlier outputs. Locking the timing output caused the already-published radio output to roll back. No staging directories remained.
- A later build starting from earlier outputs preserved the earlier audio and timing edits.
- Generated fixtures covered nested sound references, shared/cyclic reference rejection, wrong-container rejection, millisecond rounding, stereo resampling, empty audio and equivalent release-version tags.
- The standalone .NET Framework EXE scanned all 29 slots from a folder containing only the EXE. This exercised audio key loading for encrypted AWC data inside an OPEN RPF.
- The window was rendered and visually inspected after more than two seconds. The added timing selector and the existing controls were visible.

No console/game playback test was performed. DJ speech remains enabled, and cue positions follow the original track proportionally rather than being detected from the replacement music. DLC/custom layouts are checked conservatively but were not covered by the real-archive fixture above.

The localization/assignment extension adds six embedded catalogs with 160 messages each. Checks verify matching keys and placeholders, valid WAV identifiers in every language, exact requested English button names, live switching without losing selections, unique randomized assignments, clearing all assignments without clearing music, and disabling language switching while an operation is active. These checks passed on both target frameworks. All six translated layouts were visually inspected after allowing more than two seconds for rendering. The updated standalone EXE also scanned all 29 slots from a directory containing only that EXE.

Reproduction commands and the generated-fixture/integration harness are documented in `README.md`. No game assets or encryption keys are included in the repository.
