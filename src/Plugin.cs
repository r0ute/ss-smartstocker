using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MyBox;
using TMPro;
using UnityEngine;

namespace SS.src;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    internal static ConfigEntry<bool> AutofillRack;

    internal static ConfigEntry<KeyboardShortcut> ForceAutofillRackKey;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;

        AutofillRack = Config.Bind("General", "AutofillRack", true, "Enable to rack on day cycle change");

        ForceAutofillRackKey = Config.Bind("Key Bindings", "ForceAutofillRackKey",
                new KeyboardShortcut(KeyCode.E, KeyCode.LeftControl));

        Harmony harmony = new(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll(typeof(Patches));

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }


    class Patches
    {

        [HarmonyPatch(typeof(PricingProductViewer), nameof(PricingProductViewer.UpdateUnlockedProducts))]
        [HarmonyPostfix]
        static void OnUpdateUnlockedProducts(int licenseID)
        {
        }

        [HarmonyPatch(typeof(DayCycleManager), nameof(DayCycleManager.StartNextDay))]
        [HarmonyPostfix]
        static void OnNextDay()
        {
        }

        [HarmonyPatch(typeof(DayCycleManager), "Start")]
        [HarmonyPostfix]
        static void OnDayStart()
        {
        }

        [HarmonyPatch(typeof(DayCycleManager), "Update")]
        [HarmonyPostfix]
        static void OnDayUpdate()
        {
            if (ForceAutofillRackKey.Value.IsDown())
            {
                AutofillRack(false);
            }
        }


        [HarmonyPatch(typeof(PricingItem), nameof(PricingItem.Setup))]
        [HarmonyPostfix]
        static void OnPricingItemSetup(Pricing data, ref PricingItem __instance)
        {


        }

        private static void AutofillRack(bool auto = true)
        {
            if (auto && !Plugin.AutofillRack.Value)
            {
                return;
            }


            if (!auto)
            {
                Singleton<SFXManager>.Instance.PlayCoinSFX();
            }

            Logger.LogInfo($"Rack autofill finished: auto={auto}");

        }


    }
}
