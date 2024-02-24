using PieceManager;
using UnityEngine;

namespace RadioStation.RadioStation;

public static class LoadAssets
{
    public static GameObject RadioGUI = null!;
    public static GameObject GUI_Item = null!;
    public static void InitPieces()
    {
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
    }

    public static void InitGUI()
    {
        RadioGUI = RadioStationPlugin._assets.LoadAsset<GameObject>("radio_gui");
        GUI_Item = RadioStationPlugin._assets.LoadAsset<GameObject>("audio_item");
    }
}