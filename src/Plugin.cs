using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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

    internal static ConfigEntry<bool> AutoStock;

    internal static ConfigEntry<KeyboardShortcut> ForceAutoStockKey;

    internal static ConfigEntry<float> StockMultiplier;

    static readonly string PRODUCT_SURPLUS_COLOR = "#00ff0080";

    static readonly string PRODUCT_DEFICIT_COLOR = "#ff000080";

    static readonly string MAX_PRODUCT_COLOR = "#00000080";

    static readonly float FONT_SIZE_MULTIPLIER = 0.5f;

    static readonly int TEXT_PARAGRAPH_SPACING = -10;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;

        AutoStock = Config.Bind("General", "AutoStock", false, "Enable automated stocking");

        ForceAutoStockKey = Config.Bind("Key Bindings", "ForceAutoStockKey",
                new KeyboardShortcut(KeyCode.R, KeyCode.LeftControl));

        StockMultiplier = Config.Bind("General", "StockMultiplier", 2f, new ConfigDescription(
            "The multiplier is applied to the display slot product count to calculate the final purchase amount",
                new AcceptableValueRange<float>(0.01f, 10f)));

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
            if (ForceAutoStockKey.Value.IsDown())
            {
                Logger.LogDebug($"AutoStock: ForceAutoStockKey IsDown");
                AutoStock(false);
            }
        }

        [HarmonyPatch(typeof(MarketShoppingCart), "AddProduct")]
        [HarmonyPostfix]
        static void OnMarketShoppingCartAddProduct(ItemQuantity salesItem, SalesType salesType)
        {
            if (salesType != SalesType.PRODUCT)
            {
                return;
            }

            Logger.LogDebug($"OnMarketShoppingCartAddProduct: product={salesItem.FirstItemID}");
            Singleton<RackManager>.Instance.RackSlots[salesItem.FirstItemID].ForEach(UpdateLabel);
        }

        [HarmonyPatch(typeof(MarketShoppingCart), nameof(MarketShoppingCart.RemoveProduct))]
        [HarmonyPostfix]
        static void OnMarketShoppingCartRemoveProduct(ItemQuantity productData, SalesType salesType)
        {

            Logger.LogDebug($"OnMarketShoppingCartRemoveProduct: product={productData.FirstItemID}");
            Singleton<RackManager>.Instance.RackSlots[productData.FirstItemID].ForEach(UpdateLabel);
        }


        [HarmonyPatch(typeof(DisplaySlot), "SetLabel")]
        [HarmonyPatch(typeof(DisplaySlot), nameof(DisplaySlot.TakeProductFromDisplay))]
        [HarmonyPatch(typeof(DisplaySlot), nameof(DisplaySlot.AddProduct))]
        [HarmonyPostfix]
        static void OnUpdateDisplaySlotLabel(ref DisplaySlot __instance)
        {
            UpdateLabel(__instance);
        }

        private static void UpdateLabel(DisplaySlot displaySlot)
        {
            if (displaySlot.Data.HasLabel || displaySlot.Data.HasProduct)
            {
                var maxProductCount = Singleton<IDManager>.Instance.ProductSO(displaySlot.Data.FirstItemID)
                    .GridLayoutInStorage.productCount;

                if (maxProductCount == displaySlot.ProductCount)
                {
                    return;
                }

                var label = Traverse.Create(displaySlot).Field("m_Label").GetValue() as Label;
                var productCountText = Traverse.Create(label).Field("m_ProductCount").GetValue() as TMP_Text;

                productCountText.paragraphSpacing = TEXT_PARAGRAPH_SPACING;
                productCountText.text = string.Format("{0}</size><br><size={1}><color={2}>{3}</color></size>",
                    displaySlot.ProductCount,
                    productCountText.fontSizeMax * FONT_SIZE_MULTIPLIER,
                    MAX_PRODUCT_COLOR,
                    maxProductCount);
            }
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.RemoveBox), [typeof(BoxData)])]
        [HarmonyPostfix]
        static void OnInventoryManagerRemoveBox(BoxData boxData)
        {

            // Logger.LogDebug($"OnInventoryManagerRemoveBox: ProductID={boxData.ProductID}");
            Singleton<RackManager>.Instance.RackSlots[boxData.ProductID].ForEach(UpdateLabel);
        }

        [HarmonyPatch(typeof(RackSlot), "Initialize")]
        [HarmonyPatch(typeof(RackSlot), "SetLabel")]
        [HarmonyPatch(typeof(RackSlot), nameof(RackSlot.RefreshLabel))]
        [HarmonyPatch(typeof(RackSlot), nameof(RackSlot.RePositionBoxes))]
        [HarmonyPostfix]
        static void OnUpdateRackSlotLabel(ref RackSlot __instance)
        {
            UpdateLabel(__instance);
        }

        private static void UpdateLabel(RackSlot rackSlot)
        {

            if (rackSlot.HasLabel && !rackSlot.Full)
            {

                var boxSurplusCount = CountCartBoxes(rackSlot) + CountStreetBoxes(rackSlot);
                var boxDeficitCount = Singleton<IDManager>.Instance.BoxSO(GetBoxId(rackSlot)).GridLayout.boxCount
                 - rackSlot.Data.BoxCount
                 - boxSurplusCount;

                string boxCountText = "";

                if (boxDeficitCount > 0)
                {
                    boxCountText += string.Format("<color={0}>{1}</color>",
                        PRODUCT_DEFICIT_COLOR,
                        boxDeficitCount);
                }

                if (boxDeficitCount > 0 && boxSurplusCount > 0)
                {
                    boxCountText += " ";
                }

                if (boxSurplusCount > 0)
                {
                    boxCountText += string.Format("<color={0}>{1}</color>",
                        PRODUCT_SURPLUS_COLOR,
                        boxSurplusCount);
                }

                if (boxCountText.IsNullOrEmpty())
                {
                    return;
                }

                var label = Traverse.Create(rackSlot).Field("m_Label").GetValue() as Label;
                var productCountText = Traverse.Create(label).Field("m_ProductCount").GetValue() as TMP_Text;

                productCountText.paragraphSpacing = TEXT_PARAGRAPH_SPACING;
                productCountText.text = string.Format("{0}</size><br><size={1}>{2}</size>",
                    rackSlot.ProductCount,
                    productCountText.fontSizeMax * FONT_SIZE_MULTIPLIER,
                    boxCountText);
            }

        }

        private static int CountStreetBoxes(RackSlot rackSlot)
        {
            return Singleton<StorageStreet>.Instance.GetAllBoxesFromStreet()
                .Count((box) => !box.IsBoxOccupied && box.Product.ID == rackSlot.Data.ProductID);
        }

        private static int CountCartBoxes(RackSlot rackSlot)
        {
            var itemQuantity = Singleton<CartManager>.Instance.CartData.ProductInCarts
                    .FirstOrDefault((itemQuantity) => itemQuantity.FirstItemID == rackSlot.Data.ProductID);

            return (itemQuantity != null)
                ? itemQuantity.FirstItemCount
                : 0;
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


        private static void AutoStock(bool auto = true)
        {
            if (auto && !Plugin.AutoStock.Value)
            {
                return;
            }

            var cartData = Singleton<CartManager>.Instance.CartData;
            cartData.ProductInCarts.Clear();

            Singleton<DisplayManager>.Instance.DisplayedProducts.ForEach(item =>
            {
                var displayProductCount = item.Value.Sum(displaySlot => displaySlot.ProductCount);

                var inventoryProductCount = 0;

                var rackSlots = new List<RackSlot>();
                Singleton<RackManager>.Instance.RackSlots.TryGetValue(item.Key, out rackSlots);
                inventoryProductCount += rackSlots.Sum(rackSlot => rackSlot.ProductCount);

                inventoryProductCount += Singleton<StorageStreet>.Instance.GetAllBoxesFromStreet()
                    .Where(box => box.Product.ID == item.Key)
                    .Sum(box => box.ProductCount);


                var boxProductCount = Singleton<IDManager>.Instance.ProductSO(item.Key).GridLayoutInBox.productCount;
                var finalAmount = Mathf.CeilToInt((displayProductCount * StockMultiplier.Value - inventoryProductCount)
                    / boxProductCount);

                if (finalAmount > 0)
                {
                    Logger.LogDebug($"AutoStock: product={Singleton<IDManager>.Instance.ProductSO(item.Key)}, displayProductCount={displayProductCount}, inventoryProductCount={inventoryProductCount},boxProductCount={boxProductCount},finalAmount={finalAmount}");

                    var price = Singleton<PriceManager>.Instance.SellingPrice(item.Key);
                    var itemQuantity = new ItemQuantity(item.Key, price)
                    {
                        FirstItemCount = finalAmount
                    };
                    Singleton<CartManager>.Instance.AddCart(itemQuantity, SalesType.PRODUCT);

                }

            });


            if (!auto)
            {
                Singleton<SFXManager>.Instance.PlayMoneyPaperSFX();
            }


            Logger.LogInfo($"Stock update finished: auto={auto}");

        }


    }
}
