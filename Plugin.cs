using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace ErenshorNpcVoice
{
    [BepInPlugin("com.example.erenshornpcvoice", "Erenshor NPC Voice", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static AudioClip OriginalHailSound;
        private static ConfigEntry<bool> DetailedLogging;

        private enum VoiceGender
        {
            Unknown,
            Male,
            Female
        }

        private enum VoiceRole
        {
            General,
            Caster,
            Martial
        }

        private class VoiceProfile
        {
            public string Name;
            public VoiceGender Gender;
            public readonly List<AudioClip> Clips = new List<AudioClip>();
        }

        private static readonly List<VoiceProfile> VoiceProfiles =
            new List<VoiceProfile>();

        private static readonly Dictionary<string, VoiceGender> GenderOverrides =
            new Dictionary<string, VoiceGender>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, VoiceGender> MeshGenderOverrides =
            new Dictionary<string, VoiceGender>(
                StringComparer.OrdinalIgnoreCase)
            {
                { "Character_Peasant", VoiceGender.Male },
                { "SM_Chr_Rider_01", VoiceGender.Female },
                { "SM_Chr_Nun_01", VoiceGender.Female },
                { "Character_Warrior", VoiceGender.Male },
                { "Character_Knight", VoiceGender.Male },
                { "SM_Chr_Monk_01", VoiceGender.Male },
                { "SM_Chr_Merchant_01", VoiceGender.Male },
                { "SM_Chr_FortuneTeller_01", VoiceGender.Female },
                { "SM_Chr_Bartender_01", VoiceGender.Male },
                { "Character_Shopkeeper", VoiceGender.Male },
                { "Character_Ghost_01", VoiceGender.Male },
            };

        private static readonly HashSet<string> GhostMeshOverrides =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Character_Ghost_01"
            };

        private static readonly Dictionary<string, int> LastClipByNpc =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> UnknownGenderLogged =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> AppearanceResearchLogged =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ProfileChoiceLogged =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        private static string PluginFolder
        {
            get
            {
                return Path.Combine(Paths.PluginPath, "ErenshorNpcVoice");
            }
        }

        private static string VoiceFolder
        {
            get
            {
                return Path.Combine(PluginFolder, "Voices");
            }
        }

        private static string OverrideFile
        {
            get
            {
                return Path.Combine(PluginFolder, "NpcVoiceOverrides.txt");
            }
        }

        private void Awake()
        {
            Logger.LogInfo("Erenshor NPC Voice loaded.");
            DetailedLogging = Config.Bind(
                "Logging",
                "EnableDetailedLogging",
                false,
                "Log per-NPC mesh details, voice choices, and interaction diagnostics.");

            LoadGenderOverrides();

            Harmony harmony = new Harmony("com.example.erenshornpcvoice");
            harmony.PatchAll();

            StartCoroutine(LoadVoiceProfiles());
            StartCoroutine(SilenceOriginalHailSound());
        }

        private void LoadGenderOverrides()
        {
            Directory.CreateDirectory(PluginFolder);

            if (!File.Exists(OverrideFile))
            {
                File.WriteAllText(
                    OverrideFile,
                    "# Add one NPC per line, using this format:\r\n" +
                    "# NPC Name=Female\r\n" +
                    "# NPC Name=Male\r\n");
            }

            foreach (string originalLine in File.ReadAllLines(OverrideFile))
            {
                string line = originalLine.Trim();

                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                int equalsIndex = line.IndexOf('=');
                if (equalsIndex < 1)
                {
                    continue;
                }

                string npcName = line.Substring(0, equalsIndex).Trim();
                string genderName = line.Substring(equalsIndex + 1).Trim();

                if (genderName.Equals("Female", StringComparison.OrdinalIgnoreCase))
                {
                    GenderOverrides[npcName] = VoiceGender.Female;
                }
                else if (genderName.Equals("Male", StringComparison.OrdinalIgnoreCase))
                {
                    GenderOverrides[npcName] = VoiceGender.Male;
                }
            }
        }

        private IEnumerator SilenceOriginalHailSound()
        {
            while (GameData.Misc == null || GameData.Misc.HailSound == null)
            {
                yield return null;
            }

            int sampleCount = 4410;
            AudioClip silence = AudioClip.Create(
                "SilentHailSound",
                sampleCount,
                1,
                44100,
                false);

            silence.SetData(new float[sampleCount], 0);

            OriginalHailSound = GameData.Misc.HailSound;

            GameData.Misc.HailSound = silence;
            GameData.Misc.ReplySound = silence;

            Logger.LogInfo("Disabled the game's built-in Hail sound.");
        }

        private IEnumerator LoadVoiceProfiles()
        {
            if (!Directory.Exists(VoiceFolder))
            {
                Logger.LogError("Voices folder not found: " + VoiceFolder);
                yield break;
            }

            string[] profileFolders = Directory.GetDirectories(VoiceFolder);
            Array.Sort(profileFolders, StringComparer.OrdinalIgnoreCase);

            foreach (string profileFolder in profileFolders)
            {
                string profileName = Path.GetFileName(profileFolder);
                VoiceGender profileGender = GetFolderGender(profileName);

                if (profileGender == VoiceGender.Unknown)
                {
                    Logger.LogWarning(
                        "Skipping voice folder because its name must include " +
                        "'male' or 'female': " + profileName);
                    continue;
                }

                string[] files = Directory.GetFiles(profileFolder, "*.mp3");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);

                VoiceProfile profile = new VoiceProfile();
                profile.Name = profileName;
                profile.Gender = profileGender;

                foreach (string file in files)
                {
                    string fileUrl = new Uri(file).AbsoluteUri;

                    using (UnityWebRequest request =
                           UnityWebRequestMultimedia.GetAudioClip(
                               fileUrl, AudioType.MPEG))
                    {
                        yield return request.SendWebRequest();

                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            Logger.LogWarning(
                                "Could not load " + file +
                                ": " + request.error);
                            continue;
                        }

                        AudioClip clip =
                            DownloadHandlerAudioClip.GetContent(request);

                        if (clip != null)
                        {
                            clip.name = Path.GetFileNameWithoutExtension(file);
                            profile.Clips.Add(clip);
                        }
                    }
                }

                if (profile.Clips.Count > 0)
                {
                    VoiceProfiles.Add(profile);

                    LogDebug(
                        "Loaded voice profile " + profile.Name +
                        " with " + profile.Clips.Count + " clips.");
                }
                else
                {
                    Logger.LogWarning(
                        "No MP3 clips found in voice folder: " + profileFolder);
                }
            }

            Logger.LogInfo(
                "Loaded " + VoiceProfiles.Count + " NPC voice profiles.");
        }

        internal static bool DetailedLoggingEnabled
        {
            get
            {
                return DetailedLogging != null && DetailedLogging.Value;
            }
        }

        internal static void LogDebug(string message)
        {
            if (DetailedLoggingEnabled)
            {
                Debug.Log("[Erenshor NPC Voice] " + message);
            }
        }

        private static VoiceGender GetFolderGender(string folderName)
        {
            string lowerName = folderName.ToLowerInvariant();

            // Check female first because the word "female" contains "male".
            if (lowerName.Contains("female"))
            {
                return VoiceGender.Female;
            }

            if (lowerName.Contains("male"))
            {
                return VoiceGender.Male;
            }

            return VoiceGender.Unknown;
        }

        internal static void PlayNpcVoice(NPC npc)
        {
            if (npc == null || VoiceProfiles.Count == 0)
            {
                return;
            }

            List<string> appearanceNames = GetAppearanceNames(npc);
            VoiceGender gender = GetNpcGender(npc, appearanceNames);
            VoiceRole role = GetNpcRole(npc, appearanceNames);

            if (DetailedLoggingEnabled &&
                AppearanceResearchLogged.Add(npc.NPCName))
            {
                LogAppearanceResearch(npc, gender, role, appearanceNames);
            }

            if (gender == VoiceGender.Unknown)
            {
                LogUnknownGender(npc, appearanceNames);

                if (OriginalHailSound != null && GameData.PlayerAud != null)
                {
                    GameData.PlayerAud.PlayOneShot(
                        OriginalHailSound,
                        GameData.PlayerAud.volume *
                        0.5f *
                        GameData.SFXVol *
                        GameData.MasterVol);
                }

                return;
            }

            VoiceProfile profile = ChooseVoiceProfile(npc, gender, role);

            if (profile == null || profile.Clips.Count == 0)
            {
                return;
            }

            if (DetailedLoggingEnabled &&
                ProfileChoiceLogged.Add(npc.NPCName))
            {
                LogDebug(
                    "Selected profile '" + profile.Name +
                    "' for " + npc.NPCName +
                    " (voice presentation=" + gender +
                    ", role=" + role + ")");
            }

            AudioClip clip = GetRandomClipForNpc(npc, profile);

            if (clip == null)
            {
                return;
            }

            NpcVoiceAudioSource voice =
                npc.GetComponent<NpcVoiceAudioSource>();

            if (voice == null)
            {
                voice = npc.gameObject.AddComponent<NpcVoiceAudioSource>();
            }

            bool isGhost = IsGhostModel(appearanceNames);

            if (voice.Reverb != null)
            {
                voice.Reverb.enabled = isGhost;
            }

            if (voice.Echo != null)
            {
                voice.Echo.enabled = isGhost;
            }

            if (voice.Source != null)
            {
                voice.Source.pitch = isGhost ? 0.92f : 1f;

                voice.Source.PlayOneShot(
                    clip,
                    GameData.SFXVol * GameData.MasterVol * 0.25f);
            }
        }

        private static VoiceProfile ChooseVoiceProfile(
            NPC npc,
            VoiceGender gender,
            VoiceRole role)
        {
            List<VoiceProfile> genderCandidates =
                new List<VoiceProfile>();

            foreach (VoiceProfile profile in VoiceProfiles)
            {
                if (gender == VoiceGender.Unknown ||
                    profile.Gender == gender)
                {
                    genderCandidates.Add(profile);
                }
            }

            if (genderCandidates.Count == 0)
            {
                genderCandidates.AddRange(VoiceProfiles);
            }

            List<VoiceProfile> roleCandidates =
                new List<VoiceProfile>();

            foreach (VoiceProfile profile in genderCandidates)
            {
                if (MatchesRoleStyle(profile.Name, role))
                {
                    roleCandidates.Add(profile);
                }
            }

            List<VoiceProfile> candidates =
                roleCandidates.Count > 0
                    ? roleCandidates
                    : genderCandidates;

            string key = npc.NPCName + "|" + gender + "|" + role;
            int index = StableHash(key) % candidates.Count;
            return candidates[index];
        }

        private static bool MatchesRoleStyle(
            string profileName,
            VoiceRole role)
        {
            string name = profileName.ToLowerInvariant();

            if (role == VoiceRole.General)
            {
                return true;
            }

            if (role == VoiceRole.Caster)
            {
                return ContainsAny(
                    name,
                    "clear",
                    "gentle",
                    "soft",
                    "smooth",
                    "informative",
                    "knowledgeable",
                    "even",
                    "warm",
                    "mature",
                    "breathy");
            }

            if (role == VoiceRole.Martial)
            {
                return ContainsAny(
                    name,
                    "firm",
                    "gravelly",
                    "forward",
                    "lively",
                    "excitable",
                    "upbeat",
                    "bright",
                    "casual");
            }

            return true;
        }

        private static VoiceGender GetNpcGender(
            NPC npc,
            List<string> appearanceNames)
        {
            // An NPC-specific override takes priority.
            VoiceGender overrideGender;
            if (GenderOverrides.TryGetValue(npc.NPCName, out overrideGender))
            {
                return overrideGender;
            }

            // Check known model names before trying to infer gender from mesh names.
            foreach (string appearanceName in appearanceNames)
            {
                VoiceGender meshGender;

                if (MeshGenderOverrides.TryGetValue(
                    appearanceName.Trim(),
                    out meshGender))
                {
                    return meshGender;
                }
            }

            // Fall back to the model's Male/Female mesh naming.
            bool foundMale = false;
            bool foundFemale = false;

            foreach (string appearanceName in appearanceNames)
            {
                string lowerName = appearanceName.ToLowerInvariant();

                if (lowerName.Contains("_female"))
                {
                    foundFemale = true;
                }
                else if (lowerName.Contains("_male"))
                {
                    foundMale = true;
                }
            }

            if (foundFemale && !foundMale)
            {
                return VoiceGender.Female;
            }

            if (foundMale && !foundFemale)
            {
                return VoiceGender.Male;
            }

            return VoiceGender.Unknown;
        }

        private static bool IsGhostModel(List<string> appearanceNames)
        {
            foreach (string appearanceName in appearanceNames)
            {
                if (GhostMeshOverrides.Contains(appearanceName.Trim()))
                {
                    return true;
                }
            }

            return false;
        }

        private static VoiceRole GetNpcRole(
            NPC npc,
            List<string> appearanceNames)
        {
            Stats stats = npc.GetComponent<Stats>();

            if (stats != null && stats.CharacterClass != null)
            {
                string className =
                    stats.CharacterClass.ClassName.ToLowerInvariant();

                if (ContainsAny(
                    className,
                    "arcanist",
                    "druid",
                    "stormcaller"))
                {
                    return VoiceRole.Caster;
                }

                if (ContainsAny(
                    className,
                    "duelist",
                    "paladin",
                    "reaver"))
                {
                    return VoiceRole.Martial;
                }
            }

            string modelInfo = npc.NPCName.ToLowerInvariant();

            foreach (string appearanceName in appearanceNames)
            {
                modelInfo += " " + appearanceName.ToLowerInvariant();
            }

            if (ContainsAny(
                modelInfo,
                "mage",
                "wizard",
                "sorcer",
                "arcanist",
                "druid",
                "stormcaller",
                "priest",
                "nun",
                "fortune teller",
                "cleric"))
            {
                return VoiceRole.Caster;
            }

            if (ContainsAny(
                modelInfo,
                "warrior",
                "soldier",
                "guard",
                "knight",
                "duelist",
                "paladin",
                "reaver",
                "fighter",
                "sergeant",
                "headsman",
                "monk"))
            {
                return VoiceRole.Martial;
            }

            return VoiceRole.General;
        }

        private static List<string> GetAppearanceNames(NPC npc)
        {
            List<string> names = new List<string>();

            // The combiner keeps references to the original renderers here.
            foreach (Component component
                     in npc.GetComponentsInChildren<Component>(true))
            {
                if (component == null ||
                    component.GetType().FullName !=
                        "MTAssets.SkinnedMeshCombiner.CombinedMeshesManager")
                {
                    continue;
                }

                var sourceMeshesField =
                    AccessTools.Field(
                        component.GetType(),
                        "sourceMeshes");

                if (sourceMeshesField == null)
                {
                    continue;
                }

                SkinnedMeshRenderer[] sourceMeshes =
                    sourceMeshesField.GetValue(component)
                        as SkinnedMeshRenderer[];

                if (sourceMeshes == null)
                {
                    continue;
                }

                foreach (SkinnedMeshRenderer source in sourceMeshes)
                {
                    if (source == null)
                    {
                        continue;
                    }

                    AddName(names, source.name);

                    if (source.sharedMesh != null)
                    {
                        AddName(names, source.sharedMesh.name);
                    }
                }
            }

            // Non-combined modular characters expose their active parts here.
            foreach (SkinnedMeshRenderer renderer
                     in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                AddName(names, renderer.name);

                if (renderer.sharedMesh != null)
                {
                    AddName(names, renderer.sharedMesh.name);
                }
            }

            return names;
        }

        private static void AddName(List<string> names, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (!names.Contains(name))
            {
                names.Add(name);
            }
        }

        private static void LogUnknownGender(
            NPC npc,
            List<string> appearanceNames)
        {
            if (!DetailedLoggingEnabled ||
                !UnknownGenderLogged.Add(npc.NPCName))
            {
                return;
            }

            string appearance = appearanceNames.Count > 0
                ? string.Join(", ", appearanceNames.ToArray())
                : "(no mesh names found)";

            LogDebug(
                "Could not determine voice presentation for NPC '" +
                npc.NPCName + "'. Add an override in " + OverrideFile +
                ". Model details: " + appearance);
        }

        private static void LogAppearanceResearch(
            NPC npc,
            VoiceGender gender,
            VoiceRole role,
            List<string> appearanceNames)
        {
            Stats stats = npc.GetComponent<Stats>();
            string className = "(none)";

            if (stats != null && stats.CharacterClass != null)
            {
                className = stats.CharacterClass.ClassName;
            }

            string meshes = appearanceNames.Count > 0
                ? string.Join(", ", appearanceNames.ToArray())
                : "(no mesh names found)";

            LogDebug(
                "Appearance research: NPC='" + npc.NPCName +
                "', Gender=" + gender +
                ", Class=" + className +
                ", Role=" + role +
                ", Meshes=[" + meshes + "]");
        }

        private static AudioClip GetRandomClipForNpc(
            NPC npc,
            VoiceProfile profile)
        {
            int count = profile.Clips.Count;

            if (count == 0)
            {
                return null;
            }

            string key = npc.NPCName;
            int index = UnityEngine.Random.Range(0, count);

            int previousIndex;
            if (count > 1 &&
                LastClipByNpc.TryGetValue(key, out previousIndex) &&
                index == previousIndex)
            {
                index = (index + UnityEngine.Random.Range(1, count)) % count;
            }

            LastClipByNpc[key] = index;
            return profile.Clips[index];
        }

        private static bool ContainsAny(
            string text,
            params string[] values)
        {
            foreach (string value in values)
            {
                if (text.Contains(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static int StableHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261;

                foreach (char character in text)
                {
                    hash ^= character;
                    hash *= 16777619;
                }

                return (int)(hash & 0x7fffffff);
            }
        }
    }

    [HarmonyPatch(
        typeof(NPCDialogManager),
        nameof(NPCDialogManager.GenericHail))]
    internal static class GenericHailPatch
    {
        private static void Postfix(NPCDialogManager __instance)
        {
            Plugin.LogDebug(
                "GenericHail patch fired on " +
                (__instance != null ? __instance.gameObject.name : "(null manager)"));

            if (__instance == null)
            {
                return;
            }

            NPC npc = __instance.GetComponent<NPC>();

            if (npc == null)
            {
                npc = __instance.GetComponentInParent<NPC>();
            }

            if (npc == null)
            {
                npc = __instance.GetComponentInChildren<NPC>(true);
            }

            if (npc == null)
            {
                Debug.LogWarning(
                    "[Erenshor NPC Voice] GenericHail ran, but no NPC component " +
                    "was found on or near " + __instance.gameObject.name);
                return;
            }

            Plugin.LogDebug(
                "Found NPC=" + npc.NPCName +
                ", SimPlayer=" + npc.SimPlayer);

            Plugin.PlayNpcVoice(npc);
        }
    }

    public class NpcVoiceAudioSource : MonoBehaviour
    {
        public AudioSource Source { get; private set; }
        public AudioReverbFilter Reverb { get; private set; }

        public AudioEchoFilter Echo { get; private set; }

        private void Awake()
        {
            Source = gameObject.AddComponent<AudioSource>();
            Source.playOnAwake = false;
            Source.spatialBlend = 1f;
            Source.volume = 1f;
            Source.minDistance = 3f;
            Source.maxDistance = 25f;
            Source.rolloffMode = AudioRolloffMode.Linear;

            Reverb = gameObject.AddComponent<AudioReverbFilter>();
            Reverb.reverbPreset = AudioReverbPreset.Cave;
            Reverb.enabled = false;

            Echo = gameObject.AddComponent<AudioEchoFilter>();
            Echo.delay = 140f;
            Echo.decayRatio = 0.12f;
            Echo.dryMix = 1f;
            Echo.wetMix = 0.08f;
            Echo.enabled = false;
        }
    }

    [HarmonyPatch(typeof(TypeText), "Update")]
    internal static class SimPlayerHailPatch
    {
        private static void Prefix(out bool __state)
        {
            __state = IsHailInput();
        }

        private static void Postfix(bool __state)
        {
            if (!__state ||
                GameData.PlayerControl == null ||
                GameData.PlayerControl.CurrentTarget == null)
            {
                return;
            }

            Character target = GameData.PlayerControl.CurrentTarget;

            // GenericHail handles this target in the game's first branch.
            if (target.GetComponent<NPCDialogManager>() != null)
            {
                return;
            }

            if (Vector3.Distance(
                    GameData.PlayerControl.transform.position,
                    target.transform.position) >= 5f)
            {
                return;
            }

            if (target.GetComponent<SimPlayer>() == null)
            {
                return;
            }

            NPC npc = target.GetComponent<NPC>();

            if (npc == null)
            {
                Debug.LogWarning(
                    "[Erenshor NPC Voice] SimPlayer hail target has no NPC component: " +
                    target.name);
                return;
            }

            Plugin.LogDebug(
                "Playing voice for hailed SimPlayer: " + npc.NPCName);

            Plugin.PlayNpcVoice(npc);
        }

        private static bool IsHailInput()
        {
            if (GameData.PlayerTyping || GameData.PlayerControl == null)
            {
                return false;
            }

            bool keyboardHail =
                UnityEngine.Input.GetKeyDown(InputManager.Greeting);

            bool gamepadHail =
                GameData.PlayerControl.usingGamepad &&
                UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.JoystickButton1) &&
                GameData.HighlightedItem == null &&
                !UnityEngine.Input.GetKey(UnityEngine.KeyCode.JoystickButton4);

            return (keyboardHail || gamepadHail) &&
                   UnityEngine.Input.GetAxis("RTrigger") < 0.1f;
        }
    }

    [HarmonyPatch(
    typeof(NPCDialogManager),
    nameof(NPCDialogManager.ParseText))]
    internal static class KeywordResponsePatch
    {
        private static void Postfix(
            NPCDialogManager __instance,
            string __0)
        {
            if (__instance == null || string.IsNullOrEmpty(__0))
            {
                return;
            }

            NPC npc = __instance.GetComponent<NPC>();

            if (npc == null || npc.CurrentAggroTarget != null)
            {
                return;
            }

            NPCDialog[] options = __instance.MyDialogOptions;

            if (options == null)
            {
                return;
            }

            string text = __0.ToLowerInvariant();

            foreach (NPCDialog dialog in options)
            {
                if (dialog == null || dialog.KeywordToActivate == null)
                {
                    continue;
                }

                // Skip this response if its required quest is not complete.
                if (dialog.RequireQuestComplete != null &&
                    !GameData.CompletedQuests.Contains(
                        dialog.RequireQuestComplete.DBName))
                {
                    continue;
                }

                foreach (string keyword in dialog.KeywordToActivate)
                {
                    if (!string.IsNullOrEmpty(keyword) &&
                        text.Contains(keyword.ToLowerInvariant()))
                    {
                        Plugin.LogDebug(
                            "Keyword response for " +
                            npc.NPCName + ": " + keyword);

                        Plugin.PlayNpcVoice(npc);
                        return;
                    }
                }
            }
        }
    }
}