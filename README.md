# Erenshor NPC Voice

Adds short voice cues when you hail NPCs or SimPlayers and when certain NPCs respond to dialogue keywords.

## Features

- Includes 30 voice profiles, with 10 interaction clips in each profile.
- Selects a consistent voice profile for each character based on available gender, model, and class information.
- Chooses a different short cue from that profile during interactions.
- Uses the game's original hail sound when the mod cannot determine a character's voice presentation.
- Adds a subtle pitch change, reverb, and echo to the recognized ghost model.

## Installation

Install through Gale or another Thunderstore-compatible mod manager. The required BepInEx pack should be installed automatically.

## Configuration

The mod creates `BepInEx/plugins/ErenshorNpcVoice/NpcVoiceOverrides.txt`. Add one character per line to set a gender manually:
NPC Name=Female
Another NPC=Male

Detailed logging is off by default. To enable it, set EnableDetailedLogging = true in:

BepInEx/config/com.github.Jhenox.erenshornpcvoice.cfg