using System;
using System.Linq;
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


        [HarmonyPatch(typeof(RackSlot), "SetLabel")]
        [HarmonyPostfix]
        static void OnRackSlotSetLabel(ref RackSlot __instance)
        {
            UpdateLabel(__instance);

        }

        [HarmonyPatch(typeof(RackSlot), nameof(RackSlot.RefreshLabel))]
        [HarmonyPostfix]
        static void OnRackSlotRefreshLabel(ref RackSlot __instance)
        {
            UpdateLabel(__instance);

        }

        [HarmonyPatch(typeof(RackSlot), nameof(RackSlot.RePositionBoxes))]
        [HarmonyPostfix]
        static void OnRackSlotRePositionBoxes(ref RackSlot __instance)
        {
            UpdateLabel(__instance);

        }

        private static void UpdateLabel(RackSlot rackSlot)
        {
            if (rackSlot.HasLabel && !rackSlot.Full)
            {
                var box = Singleton<IDManager>.Instance.BoxSO(GetBoxId(rackSlot));
                var needBoxCount = box.GridLayout.boxCount - rackSlot.Data.BoxCount;
               
                var cartItemsCount = 0;
                var itemQuantity = Singleton<CartManager>.Instance.CartData.ProductInCarts
                    .FirstOrDefault((itemQuantity) => itemQuantity.FirstItemID == rackSlot.Data.ProductID);

                if (itemQuantity != null)
                {
                    cartItemsCount = itemQuantity.FirstItemCount;
                }

                needBoxCount -= cartItemsCount;


                string boxCountText = "";

                if (needBoxCount > 0) {
                    boxCountText += string.Format("<color=red>{0}</color>", needBoxCount);
                }

                if (cartItemsCount > 0) {
                    boxCountText += string.Format(" <color=green>{0}</color>", cartItemsCount);
                }

                if (boxCountText.IsNullOrEmpty())
                {
                    return;
                }

                var label = Traverse.Create(rackSlot).Field("m_Label").GetValue() as Label;
                var productCountText = Traverse.Create(label).Field("m_ProductCount").GetValue() as TMP_Text;

                productCountText.text = string.Format("{0}</size><br><size={1}>{2}</size>",
                    rackSlot.ProductCount,
                    productCountText.fontSize * 0.6f,
                    boxCountText);
                productCountText.paragraphSpacing = -10;
            }

        }

        private static int GetBoxId(RackSlot rackSlot)
        {
            if (rackSlot.CurrentBoxID == -1)
            {
                var boxSize = Singleton<IDManager>.Instance.ProductSO(rackSlot.Data.ProductID).GridLayoutInBox.boxSize;
                return Singleton<IDManager>.Instance.Boxes.FirstOrDefault((box) => box.BoxSize == boxSize).ID;
            }

            return rackSlot.CurrentBoxID;

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
