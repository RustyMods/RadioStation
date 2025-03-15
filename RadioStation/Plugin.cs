using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LocalizationManager;
using PieceManager;
using RadioStation.RadioStation;
using RadioStation.UI;
using ServerSync;
using UnityEngine;

namespace RadioStation
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class RadioStationPlugin : BaseUnityPlugin
    {
        internal const string ModName = "RadioStation";
        internal const string ModVersion = "1.0.4";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private const string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource RadioStationLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        public enum Toggle { On = 1, Off = 0 }

        public static AssetBundle _assets = null!;
        
        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<Toggle> _FilterAudio = null!;
        public static ConfigEntry<int> _FadeDistance = null!;
        public static ConfigEntry<Toggle> _PlayOnAwake = null!;
        public static ConfigEntry<float> _MaxVolume = null!;
        public static ConfigEntry<FontManager.FontOptions> _font = null!;
        public static ConfigEntry<Vector2> _position = null!;
        public static ConfigEntry<Toggle> _onlyCustoms = null!;

        private void InitConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            _FilterAudio = config("2 - Settings", "Filter Audio", Toggle.On, "If on, plugin will filter audio list", false);
            _FadeDistance = config("2 - Settings", "Fade Distance", 10, "Set the max distance radio station can be heard", false);
            _PlayOnAwake = config("2 - Settings", "Play On Awake", Toggle.Off, "If on, the radio will play when loaded into scene");
            _MaxVolume = config("2 - Settings", "Max Volume", 1f, new ConfigDescription("Set the max volume of the radio", new AcceptableValueRange<float>(0f, 1f)));
            _font = config("2 - Settings", "Font", FontManager.FontOptions.AveriaSerifLibre, "Set font", false);
            _font.SettingChanged += FontManager.OnFontChange;
            _position = config("2 - Settings", "Panel Position", new Vector2(960f, 620f), "Set position of panel");
            _onlyCustoms = config("2 - Settings", "Only Customs", Toggle.Off,
                "If on, radio only displays custom audio");
        }

        public void Awake()
        {
            Localizer.Load();
            InitConfigs();
            _assets = GetAssetBundle("radiobundle");

            BuildPiece RadioStation = new("radiobundle", "RadioStation");
            RadioStation.Name.English("Radio"); 
            RadioStation.Description.English("A slice of modernity");
            RadioStation.RequiredItems.Add("FineWood", 20, true); 
            RadioStation.RequiredItems.Add("SurtlingCore", 2, true);
            RadioStation.RequiredItems.Add("BronzeNails", 20, true);
            RadioStation.Category.Set(BuildPieceCategory.Furniture);
            RadioStation.Crafting.Set(CraftingTable.Workbench);
            Radio component = RadioStation.Prefab.AddComponent<Radio>();
            component.m_name = RadioStation.Name.Key;
            
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        public void Update()
        {
            RadioUI.UpdateUI();
        }
        
        private static AssetBundle GetAssetBundle(string fileName)
        {
            Assembly execAssembly = Assembly.GetExecutingAssembly();
            string resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(fileName));
            using Stream? stream = execAssembly.GetManifestResourceStream(resourceName);
            return AssetBundle.LoadFromStream(stream);
        }
        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                RadioStationLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                RadioStationLogger.LogError($"There was an issue loading your {ConfigFileName}");
                RadioStationLogger.LogError("Please check your config entries for spelling and format!");
            }
        }
        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }
    }
}