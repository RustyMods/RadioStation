using System;
using BepInEx;
using RadioStation.Managers;
using UnityEngine;

namespace RadioStation.RadioStation;

public class Radio : MonoBehaviour, Interactable, Hoverable
{
    public string m_name = "$piece_radiostation";
    public static readonly int hash = "RadioHash".GetStableHashCode();
    public static readonly int loop = "RadioLoop".GetStableHashCode();
    
    public AudioSource _audioSource = null!;
    public ZNetView _znv = null!;

    private void Awake()
    {
        _znv = GetComponent<ZNetView>();
        _audioSource = GetComponent<AudioSource>();
        if (!_znv.IsValid()) return;
        _znv.Register<string>(nameof(RPC_SetAudioClip),RPC_SetAudioClip);
        _znv.Register<bool>(nameof(RPC_SetLoop),RPC_SetLoop);
    }

    private void Start()
    {
        if (!_znv.IsValid()) return;
        if (RadioStationPlugin._PlayOnAwake.Value is RadioStationPlugin.Toggle.Off) return;
        string audioName = _znv.GetZDO().GetString(hash);
        if (!audioName.IsNullOrWhiteSpace())
        {
            if (_audioSource.clip == null)
            {
                AudioManager.AudioClips.TryGetValue(audioName, out AudioClip audioClip);
                if (!audioClip) return;
                _audioSource.clip = audioClip;
                _audioSource.Play();
            }
            else
            {
                _audioSource.Play();
            }
        }
    }

    private void Update()
    {
        CheckLoopSetting();
        if (!_audioSource.isPlaying) return;
        ControlVolume();
        CheckAudioChange();
    }

    private void ControlVolume()
    {
        if (!Player.m_localPlayer) return;
        float distance = Vector3.Distance(Player.m_localPlayer.transform.position, transform.position);
        _audioSource.volume = Mathf.Lerp(1f, 0f, distance / RadioStationPlugin._FadeDistance.Value);
    }

    private void CheckAudioChange()
    {
        if (!_znv.IsValid()) return;
        string audioName = _znv.GetZDO().GetString(hash);
        if (_audioSource.clip.name == audioName) return;
        AudioManager.AudioClips.TryGetValue(audioName, out AudioClip audioClip);
        if (!audioClip)
        {
            AudioManager.CustomAudio.TryGetValue(audioName, out AudioClip customAudio);
            if (!customAudio) return;
            _audioSource.Stop();
            _audioSource.clip = customAudio;
            _audioSource.Play();
            return;
        };
        _audioSource.Stop();
        _audioSource.clip = audioClip;
        _audioSource.Play();
    }

    private void CheckLoopSetting()
    {
        if (!_znv.IsValid()) return;
        bool flag = _znv.GetZDO().GetBool(loop);
        if (_audioSource.loop == flag) return;
        _audioSource.loop = flag;
    }

    private void RPC_SetLoop(long sender, bool value)
    {
        if (!_znv.IsValid() || !_znv.IsOwner()) return;
        _znv.GetZDO().Set(loop, value);
    }

    private void RPC_SetAudioClip(long sender, string value)
    {
        if (!_znv.IsValid() || !_znv.IsOwner()) return;
        _znv.GetZDO().Set(hash, value);
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold) return false;
        return alt ? ShowUI() : ToggleRadio();
    }

    private bool ShowUI()
    {
        if (!_znv.IsValid()) return false;
        UI.ShowUI(_znv);
        return true;
    }

    private bool ToggleRadio()
    {
        if (_audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
        else
        {
            if (_audioSource.clip == null)
            {
                string audioName = _znv.GetZDO().GetString(hash);
                AudioManager.AudioClips.TryGetValue(audioName, out AudioClip audioClip);
                
                if (audioClip == null)
                {
                    AudioManager.CustomAudio.TryGetValue(audioName, out AudioClip customAudio);
                    if (customAudio == null) return false;
                    _audioSource.clip = customAudio;
                }
                else
                {
                    _audioSource.clip = audioClip;
                }
            }
            _audioSource.Play();
        }
        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
    
    public string GetHoverText()
    {
        string output = Localization.instance.Localize($"[<color=yellow><b>$KEY_Use</b></color>] {(_audioSource.isPlaying ? "Off" : "On")}")
                        + "\n" + Localization.instance.Localize("[<color=yellow><b>L.Shift + $KEY_Use</b></color>] Menu") ;
        if (!_znv.IsValid())
        {
            return output;
        }
        
        string isLooping = _znv.GetZDO().GetBool(loop) ? "Looping: <color=yellow>On</color>" : "Looping: <color=yellow>Off</color>";
        string audioName = _znv.GetZDO().GetString(hash);
        if (audioName.IsNullOrWhiteSpace()) return output;
        return audioName + "\n" + isLooping + "\n" + output;
    }

    public string GetHoverName() => Localization.instance.Localize(m_name);
}
