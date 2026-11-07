# Erenshor NPC Voice

A native Lunaris mod for Erenshor that adds short voice cues when you hail NPCs or SimPlayers and when certain NPCs respond to dialogue keywords.

**Version:** 0.1.5  
**Mod Loader:** Lunaris

## Features

- Includes 30 voice profiles, with 10 interaction clips in each profile (300 clips total).
- Selects a consistent voice profile for each character based on available gender, model, and class information.
- Randomly selects short interaction cues from the assigned profile, avoiding immediate repetition when possible.
- Uses the game's original hail sound when the mod cannot determine a character's voice presentation.
- Adds a subtle pitch change, reverb, and echo to the recognized ghost model.
- Supports manual NPC voice presentation overrides.
- Provides optional detailed logging for troubleshooting NPC voice assignments.

## Requirements

- Erenshor (PC)
- Lunaris mod loader

This mod is designed for native Lunaris and no longer supports BepInEx, Gale, or Thunderstore installations.

## Installation

1. Install Lunaris for Erenshor.
2. Download the latest Erenshor NPC Voice release.
3. Extract the mod files into your Erenshor `plugins` directory, preserving the folder structure.
4. Launch Erenshor.
5. Open the Lunaris mod manager and make sure **Erenshor NPC Voice** is enabled.

The installed files should have the following structure:

```text
Erenshor/
└── plugins/
    ├── ErenshorNpcVoice.dll
    └── ErenshorNpcVoice/
        ├── NpcVoiceOverrides.txt
        └── Voices/
            ├── Male_.../
            │   └── *.mp3
            └── Female_.../
                └── *.mp3
```

Each voice profile has its own folder containing MP3 interaction clips.

**Important:** Lunaris may recognize a newly installed plugin without automatically enabling it. If the mod loads but NPC sounds remain unchanged, verify that it is enabled in Lunaris.

## Configuration

### NPC Voice Overrides

The configuration file is located at:

`plugins/ErenshorNpcVoice/NpcVoiceOverrides.txt`

The mod automatically creates this file if it does not exist.

To manually specify an NPC's voice presentation, add one character per line:

```ini
NPC Name=Female
Another NPC=Male
```

NPC-specific overrides take priority over automatic gender detection.

Lines beginning with `#` are treated as comments.

### Detailed Logging

Detailed logging is disabled by default.

To enable it, open the Erenshor NPC Voice settings in Lunaris and set:

`EnableDetailedLogging = true`

When enabled, the mod records additional information about NPC appearances, voice selections, and interactions to help troubleshoot unexpected voice assignments.

## How Voice Selection Works

The mod attempts to determine a suitable voice profile using:

1. Manual NPC overrides, when configured.
2. Recognized character models and mesh information.
3. Available character class information to identify general, caster, or martial voice styles.
4. A consistent character-based selection from matching voice profiles.

Once a profile is assigned, individual interaction clips are selected randomly.

If the mod cannot determine a character's voice presentation, it falls back to Erenshor's original hail sound.

## Building from Source

The source code is available in the `src` directory.

The project targets **.NET Framework 4.8** and requires:

- Visual Studio with .NET Framework development support.
- Erenshor game assemblies.
- Lunaris.
- Harmony (`Lib.Harmony`), restored through NuGet.

Open the Visual Studio solution, restore NuGet packages, and build using **Release / Any CPU**.

Harmony is used for method patching and is not intended to be bundled with the distributed mod. The plugin uses the Harmony functionality supplied by Lunaris.

## Version History

### v0.1.5 — Native Lunaris Migration

- Migrated from BepInEx to native Lunaris plugin registration.
- Replaced BepInEx logging and configuration with Lunaris interfaces.
- Retained existing NPC voice selection and interaction functionality.
- Added plugin cleanup and Harmony patch removal.
- Updated the project to target .NET Framework 4.8.
- Removed BepInEx and Thunderstore dependencies.

## Credits

Developed by Jhenox.

Created for Erenshor using Lunaris and Harmony.
