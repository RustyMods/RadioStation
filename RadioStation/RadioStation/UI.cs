using System.Collections.Generic;
using HarmonyLib;
using RadioStation.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RadioStation.RadioStation;

public static class UI
{
    private static GameObject GUI = null!;
    private static Transform Content = null!;

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    private static class LoadRadioUI
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (!__instance) return;
            GUI = Object.Instantiate(LoadAssets.RadioGUI, __instance.transform, false);
            GUI.SetActive(false);
            Content = GUI.transform.Find("Panel/Padding/$part_contentFrame/$part_ScrollView/Viewport/$part_Content");
            
            ButtonSfx VanillaButtonSFX = __instance.m_trophiesPanel.transform.Find("TrophiesFrame/Closebutton").GetComponent<ButtonSfx>();
            Image vanillaBackground = __instance.m_trophiesPanel.transform.Find("TrophiesFrame/border (1)").GetComponent<Image>();

            Transform PartCloseButton = Utils.FindChild(GUI.transform, "$part_CloseButton");
            PartCloseButton.gameObject.AddComponent<ButtonSfx>().m_sfxPrefab = VanillaButtonSFX.m_sfxPrefab;
            if (!PartCloseButton.TryGetComponent(out Button component)) return;
            component.onClick.AddListener(HideUI);

            if (GUI.transform.Find("Panel").TryGetComponent(out Image PanelImage))
            {
                PanelImage.material = vanillaBackground.material;
            }

            LoadAssets.GUI_Item.AddComponent<ButtonSfx>().m_sfxPrefab = VanillaButtonSFX.m_sfxPrefab;
        }
    }

    public static void ShowUI(ZNetView znv)
    {
        if (!znv.IsValid()) return;
        znv.ClaimOwnership();
        GUI.SetActive(true);
        DestroyRadioItems();
        AddRadioItems(znv);
    }

    private static void AddRadioItems(ZNetView znv)
    {
        foreach (KeyValuePair<string, AudioClip> item in AudioManager.CustomAudio)
        {
            GameObject entry = Object.Instantiate(LoadAssets.GUI_Item, Content);
            if (!entry.transform.Find("$part_text").TryGetComponent(out TextMeshProUGUI component)) continue;
            component.text = item.Key;
            if (!entry.TryGetComponent(out Button buttonComponent)) return;
            buttonComponent.onClick.AddListener(() =>
            {
                if (!znv.IsValid()) return;
                znv.GetZDO().Set(Radio.hash, item.Key);
            });
        }
        foreach (KeyValuePair<string, AudioClip> item in AudioManager.AudioClips)
        {
            if (!AudioManager.IsUsefulAudio(item.Key)) continue;
            GameObject entry = Object.Instantiate(LoadAssets.GUI_Item, Content);
            if (!entry.transform.Find("$part_text").TryGetComponent(out TextMeshProUGUI component)) continue;
            component.text = item.Key;
            if (!entry.TryGetComponent(out Button buttonComponent)) return;
            buttonComponent.onClick.AddListener(() =>
            {
                if (!znv.IsValid()) return;
                znv.GetZDO().Set(Radio.hash, item.Key);
            });
        }
    }

    private static void DestroyRadioItems()
    {
        foreach (Transform item in Content)
        {
            Object.Destroy(item.gameObject);
        }
    }

    public static bool IsRadioUIVisible()
    {
        return GUI && GUI.activeInHierarchy;
    }

    private static void HideUI() => GUI.SetActive(false);

    public static void UpdateUI()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || !IsRadioUIVisible()) return;
        HideUI();
    }
}