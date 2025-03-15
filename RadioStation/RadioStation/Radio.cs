using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using RadioStation.Managers;
using RadioStation.UI;
using UnityEngine;
using YamlDotNet.Serialization;

namespace RadioStation.RadioStation;

public class Radio : MonoBehaviour, Interactable, Hoverable
{
    public string m_name = "$piece_radiostation";
    private static readonly int hash = "RadioHash".GetStableHashCode();
    public static readonly int m_loopKey = "RadioLoop".GetStableHashCode();
    private static readonly int m_queueKey = "RadioPlaylist".GetStableHashCode();
    private static readonly int m_shuffleKey = "RadioShuffle".GetStableHashCode();
    public AudioSource m_audioSource = null!;
    public ZNetView m_nview = null!;

    public Queue<string> m_queue = new();
    public List<string> m_playedClips = new();
    private bool m_updatePlaylist;
    public bool m_updateProgress;
    public string m_currentSong = "";
    private float m_updatePlaylistTimer;
    private float m_currentSongLength;
    private float m_pausedElapsedTime;
    private static readonly List<Radio> m_instances = new();

    private void Awake()
    {
        if (RadioStationPlugin._PlayOnAwake.Value is RadioStationPlugin.Toggle.On) m_updatePlaylist = true;
        m_nview = GetComponent<ZNetView>();
        m_audioSource = GetComponent<AudioSource>();
        if (!m_nview.IsValid()) return;
        m_nview.Register<string>(nameof(RPC_SetAudioClip),RPC_SetAudioClip);
        m_nview.Register<bool>(nameof(RPC_SetLoop),RPC_SetLoop);
        m_nview.Register<bool>(nameof(RPC_SetShuffle), RPC_SetShuffle);
        
        m_instances.Add(this);
    }

    private void Start()
    {
        if (!m_nview.IsValid()) return;
        var data = m_nview.GetZDO().GetString(m_queueKey);
        if (data.IsNullOrWhiteSpace()) return;
        try
        {
            var deserializer = new DeserializerBuilder().Build();
            var list = deserializer.Deserialize<List<string>>(data);
            m_queue = new Queue<string>(list);
        }
        catch
        {
            // ignored
        }
    }

    private void Update()
    {
        float dt = Time.time;
        if (m_updatePlaylist)
        {
            UpdateQueue(dt);
        }

        if (m_updateProgress)
        {
            UpdateProgress(dt);
        }

        if (m_audioSource.isPlaying)
        {
            ControlVolume();
        }
    }
    
    private void OnDestroy()
    {
        SaveCurrentQueue();
        m_instances.Remove(this);
    }

    public void Queue(string musicName) => m_queue.Enqueue(musicName);
    public void Remove(string audioName)
    {
        m_queue = new Queue<string>(m_queue.Where(x => x != audioName));
    }
    
    public void PlayClip(string audioName)
    {
        if (GetAudioClip(audioName) is not { } clip) return;
        m_audioSource.clip = clip ;
        m_currentSongLength = clip.length;
        m_audioSource.Play();
        m_currentSong = audioName;
        RadioUI.m_instance.SetCurrentPlaying(audioName);
        m_updateProgress = true;
        m_playedClips.Add(audioName);
    }

    private float GetRemainingTime()
    {
        return m_currentSongLength - m_audioSource.time;
    }
    public void SaveCurrentQueue()
    {
        if (m_queue.Count == 0) return;
        if (!m_nview.IsValid()) return;
        var serializer = new SerializerBuilder().Build();
        var data = serializer.Serialize(m_queue.ToList());
        m_nview.GetZDO().Set(m_queueKey, data);
    }

    private static AudioClip? GetAudioClip(string audioName)
    {
        if (AudioManager.CustomAudio.TryGetValue(audioName, out AudioClip customAudio)) return customAudio;
        if (AudioManager.AudioClips.TryGetValue(audioName, out AudioClip audioClip)) return audioClip;
        return null;
    }

    private void UpdateQueue(float dt)
    {
        if (m_queue.Count <= 0)
        {
            if (IsPlaying())
            {
                if (IsLooping() || m_currentSong.IsNullOrWhiteSpace()) return;
                if (!(GetRemainingTime() < 1f)) return;
                RadioUI.m_instance.SetPlayIcon();
                RadioUI.m_instance.SetCurrentPlaying("");
                RadioUI.m_instance.SetProgress(0f);
                m_currentSong = "";
                m_updatePlaylist = false;
                m_updateProgress = false;
                RadioUI.m_instance.UpdateElements();
            }
            else
            {
                if (!m_currentSong.IsNullOrWhiteSpace())
                {
                    PlayClip(m_currentSong);
                    RadioUI.m_instance.UpdateElements();
                }
                else
                {
                    RadioUI.m_instance.SetPlayIcon();
                    RadioUI.m_instance.SetCurrentPlaying("");
                    RadioUI.m_instance.SetProgress(0f);
                    m_currentSong = "";
                    m_updatePlaylist = false;
                    m_updateProgress = false;
                    RadioUI.m_instance.UpdateElements();
                }
                RadioUI.m_instance.SetPlayIcon();
            }
        }
        else
        {
            m_updatePlaylistTimer += dt;
            if (m_updatePlaylistTimer < 1f) return;
            m_updatePlaylistTimer = 0.0f;
            
            if (!IsPlaying())
            {
                StartNextSong();
            }
            else
            {
                if (GetRemainingTime() > 0) return;
                StartNextSong();
            }
        }
    }
    private void UpdateProgress(float dt)
    {
        if (!IsPlaying()) return;
        var time = GetRemainingTime();
        RadioUI.m_instance.SetProgress(1f - time / m_currentSongLength);
    }

    public void StartNextSong()
    {
        if (m_queue.Count <= 0) return;
        RadioUI.m_currentRadio = this;
        string musicName = IsShuffling() ? m_queue.ToList()[Random.Range(0, m_queue.Count)] : m_queue.Dequeue();

        if (!RadioUI.m_instance.m_queueElements.TryGetValue(musicName, out RadioElement element)) return;
        RadioUI.m_instance.DeQueue(element, IsShuffling());
        if (IsLooping())
        {
            if (RadioUI.m_instance.m_playlistElements.TryGetValue(musicName, out element))
            {
                RadioUI.m_instance.Queue(element);
            }
        }
        PlayClip(musicName);
        RadioUI.m_instance.UpdateElements();
        RadioUI.m_instance.SetPlayIcon();
        RadioUI.m_instance.Resize();
        SaveCurrentQueue();
    }
    public void ControlGlobalAudio()
    {
        if (!m_audioSource.isPlaying) return;
        AudioMan.instance.m_ambientVol = 0f;
    }

    private bool IsAnotherRadioPlaying()
    {
        Radio? closestRadio = GetClosestRadio(this, transform.position, RadioStationPlugin._FadeDistance.Value);
        if (closestRadio == null) return false;
        return closestRadio.m_audioSource.isPlaying;
    }

    private void ControlVolume()
    {
        if (!Player.m_localPlayer) return;
        float distance = Vector3.Distance(Player.m_localPlayer.transform.position, transform.position);
        m_audioSource.volume = Mathf.Lerp(RadioStationPlugin._MaxVolume.Value, 0f, distance / RadioStationPlugin._FadeDistance.Value);
    }

    public bool IsLooping() => m_nview.IsValid() && m_nview.GetZDO().GetBool(m_loopKey);
    public void ToggleLoop() => m_nview.InvokeRPC(nameof(RPC_SetLoop), !IsLooping());
    public void Loop(bool enable) => m_nview.InvokeRPC(nameof(RPC_SetLoop), enable);

    private void RPC_SetLoop(long sender, bool value)
    {
        if (!m_nview.IsValid() || !m_nview.IsOwner()) return;
        m_nview.GetZDO().Set(m_loopKey, value);
    }

    private void RPC_SetAudioClip(long sender, string value)
    {
        if (!m_nview.IsValid() || !m_nview.IsOwner()) return;
        m_nview.GetZDO().Set(hash, value);
    }

    public bool IsShuffling() => m_nview.IsValid() && m_nview.GetZDO().GetBool(m_shuffleKey);
    public void ToggleShuffle() => Shuffle(!IsShuffling());
    public void Shuffle(bool enable) => m_nview.InvokeRPC(nameof(RPC_SetShuffle), enable);

    private void RPC_SetShuffle(long sender, bool value)
    {
        if (!m_nview.IsValid() || !m_nview.IsOwner()) return;
        m_nview.GetZDO().Set(m_shuffleKey, value);
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold) return false;
        return alt ? OpenUI() : TogglePlay();
    }

    private bool OpenUI()
    {
        if (!m_nview.IsValid()) return false;
        RadioUI.m_instance.Show(this);
        return true;
    }

    public void Play()
    {
        m_audioSource.time = m_pausedElapsedTime;
        m_updatePlaylist = true;
        m_updateProgress = true;
        m_pausedElapsedTime = 0f;
    }

    public bool IsPlaying() => m_audioSource.isPlaying;

    public void Pause()
    {
        m_pausedElapsedTime = m_audioSource.time;
        m_audioSource.Stop();
        m_updatePlaylist = false;
        m_updateProgress = false;
        RadioUI.m_instance.SetPlayIcon();
    }

    private bool TogglePlay()
    {
        if (IsPlaying()) Pause();
        else Play();
        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
    
    public string GetHoverText()
    {
        if (!m_nview.IsValid()) return "";
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append($"{m_currentSong}\n");
        stringBuilder.AppendFormat("[<color=yellow><b>$KEY_Use</b></color>] {0}\n", m_audioSource.isPlaying ? "Off" : "On");
        stringBuilder.Append("[<color=yellow><b>L.Shift + $KEY_Use</b></color>] Menu\n");
        var looping = m_nview.GetZDO().GetBool(m_loopKey);
        stringBuilder.AppendFormat("Looping: <color={1}>{0}</color>", looping ? "On" : "Off", looping ? "green" : "red");

        return Localization.instance.Localize(stringBuilder.ToString());
    }

    public string GetHoverName() => m_name;
    
    private static Radio? GetClosestRadio(Radio self, Vector3 point, float maxRange)
    {
        Radio? closestRadio = null;
        float num1 = 999999f;
        foreach (Radio radio in m_instances)
        {
            if (radio == self) continue;
            float num2 = Vector3.Distance(radio.transform.position, point);
            if (num2 < num1 && num2 < maxRange)
            {
                num1 = num2;
                closestRadio = radio;
            }
        }

        return closestRadio;
    }
}
