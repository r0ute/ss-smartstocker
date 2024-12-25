using System;
using System.Collections;
using System.Collections.Generic;
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

    internal static ConfigEntry<KeyboardShortcut> ToggleAllRestockersKey;

    internal static ConfigEntry<bool> DisplayLabelInfo;

    internal static ConfigEntry<bool> RackLabelInfo;

    internal static ConfigEntry<float> RackStockMultiplier;

    internal static ConfigEntry<float> ProtectedFunds;

    internal static ConfigEntry<Color> RackBoxTotalColor;

    internal static ConfigEntry<Color> RackFillBoxCountColor;

    internal static ConfigEntry<Color> DisplayProductTotalColor;

    static readonly Color DEFICIT_COLOR = new(0.5f, 0, 0, 0.5f);

    static readonly Color TOTAL_COLOR = new(0, 0, 0, 0.5f);

    static readonly float FONT_SIZE_MULTIPLIER = 0.5f;

    static readonly int TEXT_PARAGRAPH_SPACING = -10;

    static readonly float PURCHASE_DELAY_IN_SEC = 1.5f;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;

        AutoStock = Config.Bind("*General*", "AutoStock", true, "Enable automated stocking");

        RackStockMultiplier = Config.Bind("*General*", "RackStockMultiplier", 1.5f, new ConfigDescription(
            "The multiplier is applied to the display slot product count to calculate the final purchase amount",
                new AcceptableValueRange<float>(0.01f, 3f)));

        ProtectedFunds = Config.Bind("*General*", "ProtectedFunds,$", 500f, new ConfigDescription(
            "Reserved amount of money set aside and inaccessible for restocking",
                new AcceptableValueRange<float>(0f, 100_000f)));

        ForceAutoStockKey = Config.Bind("Key Bindings", "ForceAutoStockKey",
            new KeyboardShortcut(KeyCode.R, KeyCode.LeftControl));

        CleanShoppingCartKey = Config.Bind("Key Bindings", "CleanShoppingCartKey",
            new KeyboardShortcut(KeyCode.C, KeyCode.LeftControl));

        ToggleAllRestockersKey = Config.Bind("Key Bindings", "ToggleAllRestockersKey",
            new KeyboardShortcut(KeyCode.T, KeyCode.LeftControl));

        DisplayLabelInfo = Config.Bind("Label", "DisplayLabelInfo", true, "Show additional information on display label");
        DisplayProductTotalColor = Config.Bind("Label", "DisplayProductTotalColor", TOTAL_COLOR, "DisplayProductTotalColor");

        RackLabelInfo = Config.Bind("Label", "RackLabelInfo", true, "Show additional information on rack label");
        RackBoxTotalColor = Config.Bind("Label", "RackBoxTotalColor", TOTAL_COLOR, "RackBoxTotalColor");
        RackFillBoxCountColor = Config.Bind("Label", "RackFillBoxCountColor", DEFICIT_COLOR, "RackFillBoxCountColor");

        Harmony harmony = new(MyPluginInfo.PLUGIN_GUID);

        if (DisplayLabelInfo.Value)
        {
            harmony.PatchAll(typeof(DisplaySlotInfo));
        }

        if (RackLabelInfo.Value)
        {
            harmony.PatchAll(typeof(RackSlotInfo));
        }

        harmony.PatchAll(typeof(StockManager));

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    class StockManager
    {

        [HarmonyPatch(typeof(DayCycleManager), nameof(DayCycleManager.StartNextDay))]
        [HarmonyPostfix]
        static void OnStartNextDay(ref DayCycleManager __instance)
        {
            if (!AutoStock.Value)
            {
                return;
            }

            Logger.LogDebug($"AutoStock: OnStartNextDay");
            __instance.StartCoroutine(AutoStockProducts());
        }

        [HarmonyPatch(typeof(DayCycleManager), "Update")]
        [HarmonyPostfix]
        static void OnDayUpdate(ref DayCycleManager __instance)
        {
            if (ForceAutoStockKey.Value.IsDown())
            {
                Logger.LogDebug($"AutoStock: ForceAutoStockKey IsDown");
                __instance.StartCoroutine(AutoStockProducts(auto: false));
            }

            if (CleanShoppingCartKey.Value.IsDown())
            {
                Logger.LogDebug($"AutoStock: CleanShoppingCartKey IsDown");
                CleanMarketShoppingCart();
            }

            if (ToggleAllRestockersKey.Value.IsDown())
            {
                Logger.LogDebug($"AutoStock: ToggleAllRestockersKey IsDown");
                ToggleAllRestockers();
            }

            if (__instance.CurrentMinute >= 60)
            {
                Logger.LogInfo($"AutoStock: CurrentMinute={__instance.CurrentMinute}");
            }
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(MarketShoppingCart), "CleanCart")]
        static void CleanMarketShoppingCart(object instance) => throw new NotImplementedException();

        internal static IEnumerator AutoStockProducts(bool auto = true)
        {
            if (auto && !Plugin.AutoStock.Value)
            {
                yield break;
            }

            var cartManager = Singleton<CartManager>.Instance;

            if (cartManager.MarketShoppingCart.TooLateToOrderGoods)
            {
                Logger.LogDebug($"AutoStock: TooLateToOrderGoods");

                if (!auto)
                {
                    Singleton<ScannerDevice>.Instance.PlayAudio(true);
                }

                yield break;
            }

            CleanMarketShoppingCart();
            var productsByInventoryAmount = Singleton<DisplayManager>.Instance.DisplayedProducts.Keys
                .ToDictionary(key => key, Singleton<InventoryManager>.Instance.GetInventoryAmount);
            var sortedProductsByInventoryAmount = productsByInventoryAmount.OrderBy(item => item.Value);
            Logger.LogInfo($"AutoStock: productsByInventoryAmount={productsByInventoryAmount.Count}");
            yield return null;

            foreach (var item in sortedProductsByInventoryAmount)
            {
                if (item.Key <= 0)
                {
                    continue;
                }

                var product = Singleton<IDManager>.Instance.ProductSO(item.Key);
                var displayStorageProductCount = Singleton<DisplayManager>.Instance.DisplayedProducts[item.Key].Count
                    * product.GridLayoutInStorage.productCount;
                yield return null;

                var inventoryProductCount = item.Value;
                var boxProductCount = Singleton<IDManager>.Instance.ProductSO(item.Key).GridLayoutInBox.productCount;
                var targetProductCount = displayStorageProductCount * RackStockMultiplier.Value;
                var finalProductCount = targetProductCount - inventoryProductCount;

                if (finalProductCount <= 0)
                {
                    continue;
                }

                var finalAmount = Mathf.CeilToInt((displayStorageProductCount * RackStockMultiplier.Value
                                       - inventoryProductCount) / boxProductCount);

                Logger.LogDebug($"AutoStock: product={product},displayStorageProductCount={displayStorageProductCount}, inventoryProductCount={inventoryProductCount},targetAmount={targetProductCount},finalProductCount={finalProductCount},finalAmount={finalAmount}");
                Logger.LogDebug($"AutoStock: product={product} ({displayStorageProductCount} * {RackStockMultiplier.Value} (={targetProductCount}) - {inventoryProductCount} (={finalProductCount}) / {boxProductCount} rounds to {finalAmount}");

                var price = Singleton<PriceManager>.Instance.SellingPrice(item.Key);
                var itemQuantity = new ItemQuantity(item.Key, price)
                {
                    FirstItemCount = finalAmount
                };
                Logger.LogDebug($"AutoStock: product={product},FirstItemID={itemQuantity.FirstItemID},FirstItemCount={itemQuantity.FirstItemCount}");
                yield return null;

                cartManager.AddCart(itemQuantity, SalesType.PRODUCT);
                Singleton<ScannerDevice>.Instance.OnAddedItem?.Invoke(itemQuantity, SalesType.PRODUCT);
                Logger.LogDebug($"AutoStock: AddCart product={product}, count={itemQuantity.FirstItemCount}");
                yield return null;

                if (!HasEnoughMoney(cartManager))
                {
                    Logger.LogInfo($"AutoStock: Not enough money to purchase product={product}");
                    Purchase(cartManager, auto);
                    yield return new WaitForSeconds(PURCHASE_DELAY_IN_SEC);

                    if (!auto)
                    {
                        Singleton<ScannerDevice>.Instance.PlayAudio(true);
                    }

                    yield break;
                }


                if (cartManager.MarketShoppingCart.CartMaxed(willBeAddedMore: true))
                {
                    for (int ind = 0; ind < finalAmount; ind++)
                    {
                        cartManager.ReduceCart(itemQuantity, SalesType.PRODUCT);
                    }

                    Singleton<ScannerDevice>.Instance.OnRemoveItem?.Invoke(itemQuantity);
                    cartManager.ReduceCart(itemQuantity, SalesType.PRODUCT);
                    Logger.LogDebug($"AutoStock: ReduceCart product={product},FirstItemCount={itemQuantity.FirstItemCount}");
                    yield return null;

                    Purchase(cartManager, auto);
                    yield return new WaitForSeconds(PURCHASE_DELAY_IN_SEC);

                    cartManager.AddCart(new ItemQuantity(item.Key, price)
                    {
                        FirstItemCount = finalAmount
                    }, SalesType.PRODUCT);
                    Singleton<ScannerDevice>.Instance.OnAddedItem?.Invoke(itemQuantity, SalesType.PRODUCT);
                    Logger.LogDebug($"AutoStock: ReAddCart product={product}, count={itemQuantity.FirstItemCount}");
                    yield return null;
                }
            }

            if (cartManager.MarketShoppingCart.ItemCountInCart > 0)
            {
                Purchase(cartManager, auto);
                yield return new WaitForSeconds(PURCHASE_DELAY_IN_SEC);
            }

            Logger.LogInfo($"Stock update finished: auto={auto}");
        }

        private static void Purchase(CartManager cartManager, bool auto)
        {
            if (!HasEnoughMoney(cartManager))
            {
                return;
            }

            Logger.LogInfo($"AutoStock: Purchase ProductInCarts={cartManager.MarketShoppingCart.CartData.ProductInCarts.Count}");
            cartManager.MarketShoppingCart.Purchase(fromTablet: true);

            if (!auto)
            {
                Singleton<ScannerDevice>.Instance.PlayAudio(false);
            }

        }

        private static bool HasEnoughMoney(CartManager cartManager)
        {
            Logger.LogDebug($"AutoStock: GetTotalPrice={cartManager.MarketShoppingCart.GetTotalPrice()},CurrentShippingCost={cartManager.MarketShoppingCart.CurrentShippingCost},MinimumReserve={(float)Math.Round(ProtectedFunds.Value, 2)}");
            return Singleton<MoneyManager>.Instance.HasMoney(cartManager.MarketShoppingCart.GetTotalPrice()
                + cartManager.MarketShoppingCart.CurrentShippingCost
                + ProtectedFunds.Value);

        }

        private static void CleanMarketShoppingCart()
        {
            CleanMarketShoppingCart(Singleton<CartManager>.Instance.MarketShoppingCart);
            Singleton<TabletDevice>.Instance.CreateList();
            Logger.LogDebug($"AutoStock: CleanMarketShoppingCart");
        }

        private static void ToggleAllRestockers()
        {
            var restockersData = Traverse.Create(Singleton<EmployeeManager>.Instance).Field("m_RestockersData").GetValue() as List<int>;
            restockersData.ForEach(restockerId =>
        {
            var restocker = Singleton<EmployeeManager>.Instance.GetRestockerByID(restockerId);
            RestockerManagementData managementData = new(
                restockerID: restocker.ManagementData.RestockerID,
                isActive: !restocker.ManagementData.IsActive,
                useUnlabeledRacks: restocker.ManagementData.UseUnlabeledRacks,
                pickUpBoxGround: restocker.ManagementData.PickUpBoxGround,
                dropEmptyBox: restocker.ManagementData.DropEmptyBox,
                removeLabelRack: restocker.ManagementData.RemoveLabelRack,
                restockShelf: restocker.ManagementData.RestockShelf
            );

            Singleton<RestockerManager>.Instance.SetRestockerManagementData(managementData);
        });

            Singleton<SFXManager>.Instance.PlayMouseClickSFX();

        }
    }

    class RackSlotInfo
    {


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
            if (rackSlot.HasLabel)
            {

                var boxCountText = string.Format("<color=#{0}>{1}</color>",
                    ColorUtility.ToHtmlStringRGBA(RackBoxTotalColor.Value),
                    Singleton<IDManager>.Instance.BoxSO(GetBoxId(rackSlot)).GridLayout.boxCount);

                var fillBoxCount = Singleton<IDManager>.Instance.BoxSO(GetBoxId(rackSlot)).GridLayout.boxCount
                - rackSlot.Data.BoxCount;

                if (fillBoxCount > 0)
                {
                    boxCountText += string.Format(" <color=#{0}>-{1}</color>",
                        ColorUtility.ToHtmlStringRGBA(RackFillBoxCountColor.Value),
                        fillBoxCount);
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
                productCountText.text = string.Format("{0}</size><br><size={1}><color=#{2}>{3}</color></size>",
                    displaySlot.ProductCount,
                    productCountText.fontSizeMax * FONT_SIZE_MULTIPLIER,
                    ColorUtility.ToHtmlStringRGBA(DisplayProductTotalColor.Value),
                    maxProductCount);
            }
        }

    }

}
