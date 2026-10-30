# Erenshor NPC Voice

A BepInEx plugin that adds short voice cues when hailing NPCs or using supported dialogue keywords. It selects a consistent voice profile using the NPC's appearance, gender presentation, and class role. Unknown-gender NPCs use the game's generic hail sound. The ghost model has a reverb and subtle echo effect.

## Repository contents

This is the source-code repository. The voice MP3 files are intentionally omitted; they are distributed in the Thunderstore package. The plugin expects them under `BepInEx/plugins/ErenshorNpcVoice/Voices/`.

## Build notes

The project needs the game’s managed Unity assemblies and BepInEx assemblies. The `ErenshorNpcVoice.csproj` file uses configurable paths; update `GameManagedDir` and `BepInExCoreDir` at the top if your installations are elsewhere. Build for Release, then copy `bin/Release/ErenshorNpcVoice.dll` into the Thunderstore package at `BepInEx/plugins/ErenshorNpcVoice/`.

## Configuration

The plugin creates `BepInEx/plugins/ErenshorNpcVoice/NpcVoiceOverrides.txt` for NPC name gender overrides. Detailed diagnostic logging is off by default. Enable `EnableDetailedLogging` under the `Logging` section of `BepInEx/config/com.example.erenshornpcvoice.cfg` when investigating a model.

## Before public release

- Replace the example BepInEx plugin GUID `com.example.erenshornpcvoice` with a unique, permanent identifier.
- Set a release version and create the matching Thunderstore package.
- Choose a license for the source and confirm the rights and redistribution terms for the audio files.

This repository is a draft starter, not a released build.
