using HarmonyLib;

namespace RadioStation.RadioStation;

public static class Patches
{
    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.IsVisible))]
    private static class IsRadioUIVisible
    {
        private static void Postfix(ref bool __result)
        {
            __result |= UI.IsRadioUIVisible();
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    private static class IsRadioVisible
    {
        private static bool Prefix() => !UI.IsRadioUIVisible();
    }

    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.FixedUpdate))]
    private static class RadioPlayerControllerOverride
    {
        private static bool Prefix() => !UI.IsRadioUIVisible();
    }
}