using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RadioStation.Managers;
using RadioStation.RadioStation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RadioStation.UI;

public class RadioUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static GameObject m_item = null!;
    public static readonly Sprite m_playIcon = RadioStationPlugin._assets.LoadAsset<Sprite>("play");
    public static readonly Sprite m_pauseIcon = RadioStationPlugin._assets.LoadAsset<Sprite>("pause");
    public static readonly Sprite m_addIcon = RadioStationPlugin._assets.LoadAsset<Sprite>("playlistAdd");
    public static readonly Sprite m_removeIcon = RadioStationPlugin._assets.LoadAsset<Sprite>("playlistRemove");
    
    public static RadioUI m_instance = null!;
    public static Radio? m_currentRadio;

    public RectTransform m_rect = null!;
    public Image m_background = null!;
    public Text m_queueText = null!;
    public Text m_playlist = null!;
    public Text m_current = null!;
    public RectTransform m_queueList = null!;
    public RectTransform m_songList = null!;
    public Image m_progress = null!;
    public Image m_shuffleImage= null!;
    public Image m_playImage = null!;
    public Image m_loopImage = null!;
    public Text m_placeholder = null!;

    public readonly Dictionary<string, RadioElement> m_playlistElements = new();
    public readonly Dictionary<string, RadioElement> m_queueElements = new();
    private float m_contentHeight;
    private float m_itemHeight;
    private Vector3 m_mouseDifference;

    public void Init()
    {
        m_rect = GetComponent<RectTransform>();
        m_background = GetComponent<Image>();
        m_queueText = transform.Find("Text_Queue").GetComponent<Text>();
        m_playlist = transform.Find("Text_Playlist").GetComponent<Text>();
        m_current = transform.Find("Text_Current").GetComponent<Text>();
        m_queueList = transform.Find("Playlist/ScrollRect/Viewport/ContentList").GetComponent<RectTransform>();
        m_songList = transform.Find("Songs/ScrollRect/Viewport/ContentList").GetComponent<RectTransform>();
        m_contentHeight = transform.Find("Playlist").GetComponent<RectTransform>().sizeDelta.y;
        
        m_progress = transform.Find("ProgressBar").GetComponent<Image>();
        var m_shuffle = transform.Find("Buttons/ShuffleButton").GetComponent<Button>();
        var m_previous = transform.Find("Buttons/PreviousButton").GetComponent<Button>();
        var m_play = transform.Find("Buttons/PlayButton").GetComponent<Button>();
        var m_next = transform.Find("Buttons/NextButton").GetComponent<Button>();
        var m_loop = transform.Find("Buttons/LoopButton").GetComponent<Button>();
        var m_close = transform.Find("Buttons/CloseButton").GetComponent<Button>();
        var m_searchField = transform.Find("SearchField").GetComponent<InputField>();
        m_placeholder = m_searchField.transform.Find("Placeholder").GetComponent<Text>();
        
        m_shuffleImage = m_shuffle.transform.Find("Icon").GetComponent<Image>();
        m_playImage = m_play.transform.Find("Icon").GetComponent<Image>();
        m_loopImage = m_loop.transform.Find("Icon").GetComponent<Image>();
        
        var item = RadioStationPlugin._assets.LoadAsset<GameObject>("audio_item");
        item.AddComponent<RadioElement>();
        m_item = item;
        m_itemHeight = item.GetComponent<RectTransform>().sizeDelta.y + m_queueList.GetComponent<VerticalLayoutGroup>().spacing;
        m_shuffle.onClick.AddListener(OnShuffle);
        m_previous.onClick.AddListener(OnPrevious);
        m_play.onClick.AddListener(OnPlay);
        m_next.onClick.AddListener(OnNext);
        m_loop.onClick.AddListener(OnLoop);
        m_close.onClick.AddListener(OnClose);
        m_searchField.onValueChanged.AddListener(OnFilter);

        FontManager.SetFont(GetComponentsInChildren<Text>());
        FontManager.SetFont(m_item.GetComponentsInChildren<Text>());
        
        var sfx = InventoryGui.instance.transform.Find("root/Trophies/TrophiesFrame/Closebutton").GetComponent<ButtonSfx>().m_sfxPrefab;
        foreach (var button in GetComponentsInChildren<Button>())
        {
            button.gameObject.AddComponent<ButtonSfx>().m_sfxPrefab = sfx;
        }

        foreach (var button in m_item.GetComponentsInChildren<Button>())
        {
            button.gameObject.AddComponent<ButtonSfx>().m_sfxPrefab = sfx;
        }
        
        m_instance = this;
        Hide();
        SetProgress(0f);
        SetCurrentPlaying("");
        SetPlaceholderText("$text_search");
        SetPlaylistText("$text_playlist");
        SetQueueText("$text_queue");

        m_rect.position = RadioStationPlugin._position.Value;
    }

    public static void UpdateUI()
    {
        if (!Player.m_localPlayer || !m_instance) return;
        if (Input.GetKeyDown(KeyCode.Escape) && IsVisible()) m_instance.Hide();
    }

    public void Hide()
    {
        if (m_currentRadio is not null) m_currentRadio.SaveCurrentQueue();
        gameObject.SetActive(false);
        Clear();
    }

    public void Show(Radio radio)
    {
        if (!radio.m_nview.IsValid()) return;
        m_currentRadio = radio;
        m_currentRadio.m_nview.ClaimOwnership();
        gameObject.SetActive(true);

        LoadPlaylist();
        LoadQueue();

        Resize();
        
        SetLoopIcon();
        SetShuffleIcon();
        SetPlayIcon();
    }

    private static bool IsVisible() => m_instance && m_instance.gameObject.activeInHierarchy;

    private void LoadPlaylist()
    {
        if (RadioStationPlugin._onlyCustoms.Value is RadioStationPlugin.Toggle.Off)
        {
            foreach (var kvp in AudioManager.CustomAudio)
            {
                Add(kvp.Key, kvp.Value);
            }
        }

        foreach (var kvp in AudioManager.AudioClips)
        {
            if (!AudioManager.IsUsefulAudio(kvp.Key)) continue;
            Add(kvp.Key, kvp.Value);
        }
    }

    private void LoadQueue()
    {
        if (m_currentRadio == null) return;
        foreach (var audioName in m_currentRadio.m_queue.Reverse())
        {
            if (!m_playlistElements.TryGetValue(audioName, out RadioElement element)) continue;
            Queue(element, false, false, false);
        }
    }

    public void SetBackground(Sprite? sprite) => m_background.sprite = sprite;
    public void SetQueueText(string text) => m_queueText.text = Localization.instance.Localize(text);
    public void SetPlaylistText(string text) => m_playlist.text = Localization.instance.Localize(text);
    public void SetPlaceholderText(string text) => m_placeholder.text = Localization.instance.Localize(text);
    public void SetProgress(float progress) => m_progress.fillAmount = Mathf.Clamp01(progress);

    public void SetCurrentPlaying(string audioName)
    {
        m_current.text = Localization.instance.Localize(audioName);
    }

    public void OnShuffle()
    {
        if (m_currentRadio == null) return;
        m_currentRadio.ToggleShuffle();
        SetShuffleIcon();
    }

    public void OnFilter(string value)
    {
        foreach (var element in RadioElement.m_instances)
        {
            element.gameObject.SetActive(element.m_audioName.ToLower().Contains(value.ToLower()));
        }
        Resize();
    }

    public void SetShuffleIcon()
    {
        if (m_currentRadio == null) return;
        m_shuffleImage.color = m_currentRadio.IsShuffling() ? new Color(1f, 0.5f, 0f, 1f) : Color.white;
    }

    public void OnPrevious()
    {
        if (m_currentRadio == null || m_currentRadio.m_playedClips.Count <= 0) return;
        var clipName = m_currentRadio.m_playedClips[m_currentRadio.m_playedClips.Count - 1];
        m_currentRadio.m_playedClips.Remove(clipName);
        m_currentRadio.PlayClip(clipName);
        m_currentRadio.m_playedClips.Remove(clipName);
        SetPlayIcon();
    }

    public void OnClose() => Hide();

    public void OnPlay()
    {
        if (m_currentRadio is null) return;
        if (m_currentRadio.IsPlaying())
        {
            m_currentRadio.Pause();
        }
        else
        {
            m_currentRadio.Play();
        }
    }

    public void SetPlayIcon()
    {
        if (m_currentRadio is null) return;
        m_playImage.sprite = m_currentRadio.IsPlaying() ? m_pauseIcon : m_playIcon;
    }

    public void OnNext()
    {
        if (m_currentRadio == null) return;
        m_currentRadio.StartNextSong();
    }

    public void OnLoop()
    {
        if (m_currentRadio == null) return;
        m_currentRadio.ToggleLoop();
        SetLoopIcon();
    }

    public void SetLoopIcon()
    {
        if (m_currentRadio == null) return;
        m_loopImage.color = m_currentRadio.IsLooping() ? new Color(1f, 0.5f, 0f, 1f) : Color.white;
    }

    public void Add(string audioName, AudioClip clip)
    {
        var element = Instantiate(m_item, m_songList).GetComponent<RadioElement>();
        element.SetClip(audioName, clip);
        m_playlistElements[audioName] = element;
    }

    public void Resize()
    {
        Resize(m_songList);
        Resize(m_queueList);
    }

    private void Resize(RectTransform list)
    {
        var count = 0;
        foreach (Transform child in list)
        {
            if (child.gameObject.activeSelf) ++count;
        }
        var newHeight = Mathf.CeilToInt(count * m_itemHeight);
        list.offsetMin = newHeight < m_contentHeight ? Vector2.zero : new Vector2(0f, -(newHeight - m_contentHeight));
    }

    public void Play(RadioElement source)
    {
        if (m_currentRadio == null) return;
        m_currentRadio.PlayClip(source.m_audioName);
        SetPlayIcon();
    }

    public void UpdateElements()
    {
        if (m_currentRadio is null) return;
        var currentPlaying = m_currentRadio.m_currentSong;
        foreach (var instance in RadioElement.m_instances)
        {
            if (instance.m_audioName != currentPlaying)
            {
                instance.SetPlayIcon();
                instance.m_playing = false;
            }
            else
            {
                instance.SetPauseIcon();
                instance.m_playing = true;
            }
        }
        
    }
    public void Pause()
    {
        if (m_currentRadio == null) return;
        m_currentRadio.Pause();
    }

    public void Queue(RadioElement source, bool queue = true, bool save = true, bool resize = true)
    {
        if (m_currentRadio is null) return;
        if (!Instantiate(m_item, m_queueList).TryGetComponent(out RadioElement element)) return;
        if (queue) m_currentRadio.Queue(source.m_audioName);
        element.SetClip(source.m_audioName, source.m_clip);
        element.SetRemoveIcon();
        element.m_queued = true;
        element.transform.SetSiblingIndex(0);
        m_queueElements[element.m_audioName] = element;
        m_playlistElements.Remove(element.m_audioName);
        Destroy(source.gameObject);
        if (resize) Resize();
        m_currentRadio.SaveCurrentQueue();
    }

    public void DeQueue(RadioElement source, bool updateRadio = true)
    {
        if (m_currentRadio is null) return;
        if (updateRadio) m_currentRadio.Remove(source.m_audioName);
        if (!Instantiate(m_item, m_songList).TryGetComponent(out RadioElement element)) return;
        element.SetClip(source.m_audioName, source.m_clip);
        m_playlistElements[element.m_audioName] = element;
        m_queueElements.Remove(source.m_audioName);
        Destroy(source.gameObject);
        Resize();
        m_currentRadio.SaveCurrentQueue();
    }

    public void Clear()
    {
        foreach (Transform child in m_queueList) Destroy(child.gameObject);
        foreach (Transform child in m_songList) Destroy(child.gameObject);
        m_playlistElements.Clear();
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        m_mouseDifference = m_rect.position - new Vector3(eventData.position.x, eventData.position.y, 0);
    }
    public void OnDrag(PointerEventData eventData)
    {
        m_rect.position = Input.mousePosition + m_mouseDifference;
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        RadioStationPlugin._position.Value = m_rect.position;
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    private static class InventoryGUI_Awake_Patch
    {
        private static void Postfix(InventoryGui __instance)
        {
            Instantiate(RadioStationPlugin._assets.LoadAsset<GameObject>("radio_gui"), __instance.transform).AddComponent<RadioUI>().Init();
        }
    }
    
    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.IsVisible))]
    private static class IsRadioUIVisible
    {
        private static void Postfix(ref bool __result)
        {
            __result |= IsVisible();
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    private static class IsRadioVisible
    {
        private static bool Prefix() => !IsVisible();
    }

    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.FixedUpdate))]
    private static class RadioPlayerControllerOverride
    {
        private static bool Prefix() => !IsVisible();
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.SetMapMode))]
    private static class Minimap_SetMapMode_Patch
    {
        private static bool Prefix() => !IsVisible();
    }
}