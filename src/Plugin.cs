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

    internal static ConfigEntry<bool> AutoStock;

    internal static ConfigEntry<KeyboardShortcut> ForceAutoStockKey;

    internal static ConfigEntry<KeyboardShortcut> CleanShoppingCartKey;

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

        CleanShoppingCartKey = Config.Bind("Key Bindings", "CleanShoppingCartKey",
                new KeyboardShortcut(KeyCode.C, KeyCode.LeftControl));

        StockMultiplier = Config.Bind("General", "StockMultiplier", 2f, new ConfigDescription(
            "The multiplier is applied to the display slot product count to calculate the final purchase amount",
                new AcceptableValueRange<float>(0.01f, 10f)));

        Harmony harmony = new(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll(typeof(DisplaySlotInfo));
        harmony.PatchAll(typeof(RackSlotInfo));
        harmony.PatchAll(typeof(StockManager));

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    public void Update()
    {
        if (ForceAutoStockKey.Value.IsDown())
        {
            Logger.LogDebug($"AutoStock: ForceAutoStockKey IsDown");
            StockManager.AutoStockProducts(false);
        }

        if (CleanShoppingCartKey.Value.IsDown())
        {
            Logger.LogDebug($"AutoStock: CleanShoppingCartKey IsDown");
            StockManager.CleanMarketShoppingCart();
        }
    }

    class StockManager
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
            if (!AutoStock.Value)
            {
                return;
            }

            AutoStockProducts();
        }

        [HarmonyPatch(typeof(DayCycleManager), "Start")]
        [HarmonyPostfix]
        static void OnDayStart()
        {
            if (!AutoStock.Value)
            {
                return;
            }

            AutoStockProducts(true);
        }

        [HarmonyPatch(typeof(DayCycleManager), "Update")]
        [HarmonyPostfix]
        static void OnDayUpdate(ref DayCycleManager __instance)
        {


            if (__instance.CurrentMinute >= 60)
            {
                Logger.LogInfo($"AutoStock: CurrentMinute={__instance.CurrentMinute}");
            }
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(MarketShoppingCart), "CleanCart")]
        static void CleanMarketShoppingCart(object instance) => throw new NotImplementedException();

        internal static void AutoStockProducts(bool auto = true)
        {
            if (auto && !Plugin.AutoStock.Value)
            {
                return;
            }

            CleanMarketShoppingCart();

            Singleton<DisplayManager>.Instance.DisplayedProducts
                .OrderBy(item => Singleton<InventoryManager>.Instance.GetInventoryAmount(item.Key))
                .ForEach(item =>
                {
                    var product = Singleton<IDManager>.Instance.ProductSO(item.Key);
                    var displayStorageProductCount = item.Value.Sum(displaySlot => product.GridLayoutInStorage.productCount);

                    var inventoryProductCount = Singleton<InventoryManager>.Instance.GetInventoryAmount(item.Key);
                    var boxProductCount = Singleton<IDManager>.Instance.ProductSO(item.Key).GridLayoutInBox.productCount;

                    var targetProductCount = displayStorageProductCount * StockMultiplier.Value;
                    var finalProductCount = targetProductCount - inventoryProductCount;

                    if (finalProductCount <= 0)
                    {
                        return;
                    }

                    var finalAmount = Mathf.CeilToInt((displayStorageProductCount * StockMultiplier.Value - inventoryProductCount)
                        / boxProductCount);

                    Logger.LogDebug($"AutoStock: product={product}, displayStorageProductCount={displayStorageProductCount}, inventoryProductCount={inventoryProductCount},targetAmount={targetProductCount},finalProductCount={finalProductCount},finalAmount={finalAmount}");
                    Logger.LogDebug($"AutoStock: product={product} ({displayStorageProductCount} * {StockMultiplier.Value} (={targetProductCount}) - {inventoryProductCount} (={finalProductCount}) / {boxProductCount} rounds to {finalAmount}");

                    var price = Singleton<PriceManager>.Instance.SellingPrice(item.Key);
                    var itemQuantity = new ItemQuantity(item.Key, price)
                    {
                        FirstItemCount = finalAmount
                    };

                    Logger.LogDebug($"AutoStock: product={product}, FirstItemID={itemQuantity.FirstItemID}, FirstItemCount={itemQuantity.FirstItemCount}");
                    var cartManager = Singleton<CartManager>.Instance;

                    cartManager.AddCart(itemQuantity, SalesType.PRODUCT);
                    Singleton<ScannerDevice>.Instance.OnAddedItem?.Invoke(itemQuantity, SalesType.PRODUCT);

                    if (cartManager.MarketShoppingCart.GetHasMoneyForPurchase())
                    {
                        Logger.LogInfo($"AutoStock: Purchased product={product}, quantity={itemQuantity.FirstItemCount}");
                        cartManager.MarketShoppingCart.Purchase();
                    }
                    else
                    {
                        cartManager.ReduceCart(itemQuantity, SalesType.PRODUCT);
                        Singleton<ScannerDevice>.Instance.PlayAudio(true);
                        Logger.LogInfo($"AutoStock: Not enough money to purchase product={product}");
                    }
                });

            if (!auto)
            {
                Singleton<ScannerDevice>.Instance.PlayAudio(false);
            }

            Logger.LogInfo($"Stock update finished: auto={auto}");

        }

        internal static void CleanMarketShoppingCart()
        {
            CleanMarketShoppingCart(Singleton<CartManager>.Instance.MarketShoppingCart);
        }

    }

    class RackSlotInfo
    {

        [HarmonyPatch(typeof(MarketShoppingCart), "AddProduct")]
        [HarmonyPostfix]
        static void OnMarketShoppingCartAddProduct(ItemQuantity salesItem, SalesType salesType)
        {
            if (salesType != SalesType.PRODUCT)
            {
                return;
            }

            Singleton<RackManager>.Instance.RackSlots[salesItem.FirstItemID].ForEach(UpdateLabel);
        }

        [HarmonyPatch(typeof(MarketShoppingCart), nameof(MarketShoppingCart.RemoveProduct))]
        [HarmonyPostfix]
        static void OnMarketShoppingCartRemoveProduct(ItemQuantity productData, SalesType salesType)
        {

            if (salesType != SalesType.PRODUCT)
            {
                return;
            }

            Singleton<RackManager>.Instance.RackSlots[productData.FirstItemID].ForEach(UpdateLabel);
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.RemoveBox), [typeof(BoxData)])]
        [HarmonyPostfix]
        static void OnInventoryManagerRemoveBox(BoxData boxData)
        {

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

    }

    class DisplaySlotInfo
    {

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

    }

}
