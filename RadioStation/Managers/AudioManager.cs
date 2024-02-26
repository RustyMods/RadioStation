using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace RadioStation.Managers;

public static class AudioManager
{
    private static readonly string FolderPath = Paths.ConfigPath + Path.DirectorySeparatorChar + "RadioStation";
    private static readonly string FilePath = FolderPath + Path.DirectorySeparatorChar + "AllAudio.yml";
    private static readonly string UsefulFilePath = FolderPath + Path.DirectorySeparatorChar + "AudioList.yml";
    private static readonly string CustomAudioFolderPath = FolderPath + Path.DirectorySeparatorChar + "CustomAudio";

    public static readonly Dictionary<string, AudioClip> AudioClips = new();
    public static readonly Dictionary<string, AudioClip> CustomAudio = new();

    private static HashSet<string> UsefulAudio = new()
    {
        "Amb_Caves_IceBreak_01",
        "Amb_Caves_WolfHowl_05",
        "Magic_CollectorLoop2",
        "Quake_EarthCrack",
        "Enemy_Gjall_Attack_Taunt_02",
        "Enemy_Gjall_Idle_Vocal_01",
        "Mountains - Abandoned cottages",
        "Amb_ShipMain_Small_S_Loop",
        "Boss 6 Queen Ambience",
        "Enemy_Abomination_VocalsIdle_S_06",
        "Black Forest(day)",
        "ForestIsMovingLv3",
        "Amb_Caves_WolfHowl_09",
        "Insect_FlySwarmLoop1",
        "Enemy_Gjall_Idle_Vocal_03",
        "HiveQueen_Idle_05",
        "Boss 5 Yagluth",
        "15 Rookery, Nests Of Rookery, With N",
        "ForestIsMovingLv1",
        "Lava_EruptRumble_Loop",
        "Plains - Sealed Tower",
        "UI_Vomit_Female_M_03",
        "Scrape_Stone_Loop",
        "Plains - Fuling Camp",
        "Mistlands - Dvergr Excavation Site",
        "Insect_WaspLoop",
        "Swamps",
        "Mountains",
        "Plains",
        "Meadows(day)",
        "Mountain Cave Sanctum",
        "Boss 3 Bonemass",
        "Meadows - Village & Farm",
        "Thunder03",
        "Fan_LargeLoop01",
        "Fire_Loop03",
        "22 Whitethroat, Song, With Willow Wa",
        "Ui_Click_01",
        "Fire_CampfireLoop9",
        "vo_helloviking",
        "23 Lesser Whitethroat",
        "Amb_Caves_WolfHowl_11",
        "25 Sailing Dinghy, Sailing, Constant",
        "Thunder10",
        "LocationReveal",
        "SW008_Wendland_Autumn_Wind_In_Reeds_Medium_Distance_Leaves_Only",
        "Amb_ThunderClap_S_01",
        "Amb_ThunderClap_S_02",
        "Amb_ThunderClap_S_03",
        "Amb_ThunderClap_S_04",
        "Amb_ThunderClap_S_05",
        "Amb_ThunderClap_S_06",
        "Amb_ThunderClap_S_08",
        "Amb_ThunderClap_S_07",
        "Amb_MistlandsThunder_01",
        "Amb_MistlandsThunder_02",
        "Amb_MistlandsThunder_03",
        "Amb_MistlandsThunder_04",
        "Amb_MistlandsThunder_05",
        "Amb_MistlandsThunder_06",
        "Amb_MistlandsThunder_07",
        "Amb_MistlandsThunder_08",
        "Meadows - Hildir",
    };

    private static List<string> GetAudioClipNames()
    {
        List<AudioClip> resources = Resources.FindObjectsOfTypeAll<AudioClip>().ToList();
        List<string> AudioClipNames = new();
        foreach (AudioClip clip in resources)
        {
            AudioClipNames.Add(clip.name);
            AudioClips[clip.name] = clip;
        }
        return AudioClipNames;
    }

    public static bool IsUsefulAudio(string name) => RadioStationPlugin._FilterAudio.Value is RadioStationPlugin.Toggle.Off || UsefulAudio.Contains(name);

    private static void InitAudioManager()
    {
        List<string> audioClipNames = GetAudioClipNames();
        WriteUsefulAudio();
        ReadUsefulAudio();
        RegisterCustomAudio();
        if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);
        File.WriteAllLines(FilePath, audioClipNames);
    }

    private static void WriteUsefulAudio()
    {
        if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);
        if (File.Exists(UsefulFilePath)) return;
        File.WriteAllLines(UsefulFilePath, UsefulAudio);
    }

    private static void ReadUsefulAudio()
    {
        if (!File.Exists(UsefulFilePath)) return;
        List<string> list = File.ReadAllLines(UsefulFilePath).ToList();
        HashSet<string> hashList = new();
        foreach (string item in list)
        {
            if (hashList.Contains(item)) continue;
            hashList.Add(item);
        }

        UsefulAudio = hashList;
    }

    private static void RegisterCustomAudio()
    {
        if (!Directory.Exists(CustomAudioFolderPath)) Directory.CreateDirectory(CustomAudioFolderPath);
        string[] files = Directory.GetFiles(CustomAudioFolderPath);
        foreach (string file in files)
        {
            GetCustomAudio(file);
        }
    }
    private static void GetCustomAudio(string file)
    {
        using UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip("file:///" + file.Replace("\\","/"), AudioType.UNKNOWN);
        webRequest.SendWebRequest();
        while (!webRequest.isDone)
        {
        }

        if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
        {
            RadioStationPlugin.RadioStationLogger.LogDebug("Failed to load audio file: " + webRequest.error);
        }
        else
        {
            DownloadHandlerAudioClip downloadHandlerAudioClip = (DownloadHandlerAudioClip)webRequest.downloadHandler;
            AudioClip clip = downloadHandlerAudioClip.audioClip;
            clip.name = Path.GetFileNameWithoutExtension(file);
            CustomAudio[clip.name] = clip;
            RadioStationPlugin.RadioStationLogger.LogDebug("Successfully added audio: " + clip.name);
        }
    }
    
    [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
    private static class SaveAudioNames
    {
        private static void Postfix() => InitAudioManager();
    }
}