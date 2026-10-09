# v0.6 - Stable

- Replacement songs now build with matching radio timing metadata, using the actual converted audio length.
- DJ and other timed radio cues move with the replacement song's length.
- The app finds the nearby base-game timing archive and lets you choose a DLC timing archive or extracted sounds REL.
- The completion window shows where to install both outputs. Installing the radio RPF alone will leave the old schedule in place.
- Builds stop when a song's timing cannot be matched safely. Failed or cancelled builds keep previous output files.
- Original sources and unrelated timing records are preserved.
- Equivalent release tags such as `v0.6` and `v0.6.0` no longer cause a false update prompt.

The timing fix uses streaming durations in `sounds.dat54.rel` and cue offsets inside the AWC. CodeWalker's dat151 radio-track settings do not expose the duration or intro/outro fields described in the original proposal.

DJ speech remains enabled. Cue positions are adapted from the original track rather than detected from the new music. Console playback validation is still required.
