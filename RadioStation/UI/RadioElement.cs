using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RadioStation.UI;

public class RadioElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public static readonly List<RadioElement> m_instances = new();
    public RectTransform m_rect = null!;
    public Text m_name = null!;
    public Button m_play = null!;
    public Image m_playIcon = null!;
    public Button m_playlist = null!;
    public Image m_playlistIcon = null!;
    public Image m_background = null!;

    public string m_audioName = "";
    public AudioClip m_clip = null!;
    public bool m_queued;
    public bool m_playing;

    public void Awake()
    {
        m_rect = GetComponent<RectTransform>();
        m_background = GetComponent<Image>();
        m_name = transform.Find("Name").GetComponent<Text>();
        m_play = transform.Find("Play").GetComponent<Button>();
        m_playIcon = transform.Find("Play").GetComponent<Image>();
        m_playlist = transform.Find("Playlist").GetComponent<Button>();
        m_playlistIcon = transform.Find("Playlist").GetComponent<Image>();

        m_play.onClick.AddListener(OnPlay);
        m_playlist.onClick.AddListener(OnQueue);
        m_instances.Add(this);
    }

    public void OnDestroy()
    {
        m_instances.Remove(this);
    }

    public void SetName(string audioName)
    {
        var text = Localization.instance.Localize(audioName);
        if (text.Length > 30)
        {
            text = text.Substring(0, 30) + "...";
        }
        m_name.text = text;
    }

    public void SetClip(string audioName, AudioClip clip)
    {
        m_audioName = audioName;
        m_clip = clip;
        var span = TimeSpan.FromSeconds(clip.length).ToString(@"m\:ss");
        SetName($"{span} - {audioName}");
    }

    public void OnPlay()
    {
        if (m_playing)
        {
            RadioUI.m_instance.Pause();
        }
        else
        {
            RadioUI.m_instance.Play(this);
        }
        SetPlayIcon();
    }

    public void SetPlayIcon() => m_playIcon.sprite = RadioUI.m_playIcon;
    public void SetPauseIcon() => m_playIcon.sprite = RadioUI.m_pauseIcon;
    public void SetAddIcon() => m_playlistIcon.sprite = RadioUI.m_addIcon;
    public void SetRemoveIcon() => m_playlistIcon.sprite = RadioUI.m_removeIcon;

    public void OnQueue()
    {
        if (m_queued) RadioUI.m_instance.DeQueue(this);
        else RadioUI.m_instance.Queue(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        m_background.color = new Color(1f, 1f, 1f, 0.1f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        m_background.color = Color.clear;
    }
    
    public static void OnFontChange(Font? font)
    {
        foreach (var instance in m_instances)
        {
            instance.m_name.font = font;
        }
    }
}