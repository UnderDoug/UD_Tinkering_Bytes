using HarmonyLib;
using Qud.UI;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.EventSystems;
using XRL;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;

namespace UD_Tinkering_Bytes.Harmony
{
    [HarmonyPatch]
    public static class TradeLine_Patches
    {
        public static bool MouseClick = false;

        public static bool Success = false;

        [HarmonyPatch(
            declaringType: typeof(TradeLine),
            methodName: nameof(TradeLine.OnPointerClick),
            argumentTypes: new Type[] { typeof(PointerEventData) },
            argumentVariations: new ArgumentType[] { ArgumentType.Normal })]
        [HarmonyPrefix]
        public static bool OnPointerClick_EventInstead_Prefix(ref TradeLine __instance, ref PointerEventData eventData)
        {
            if (__instance.context.IsActive() && !TradeLine.dragging)
            {
                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    MouseClick = true;
                }
                else
                {
                    MouseClick = false;
                }
            }
            return true;
        }

        [HarmonyPatch(
            declaringType: typeof(TradeLine),
            methodName: nameof(TradeLine.HandleVendorActions))]
        [HarmonyPrefix]
        public static bool HandleVendorActions_EventInstead_Prefix(ref TradeLine __instance)
        {
            HandleVendorActions(TradeScreen.Trader, __instance);

            MouseClick = false;
            bool success = Success;
            if (success)
            {
                // potentially do something here?
            }
            Success = false;
            return false; // skip the patched method. 
        }

        public static async void HandleVendorActions(GameObject Vendor, TradeLine TradeLine)
        {
            GameObject item = TradeLine.context.data.go;
            if (item != null)
            {
                Dictionary<string, VendorAction> actions = new();
                GetVendorActionsEvent.Send(TradeLine, Vendor, item, actions, true);
                
                VendorAction action = null;
                await APIDispatch.RunAndWaitAsync(delegate
                {
                    action = VendorAction.ShowVendorActionMenu(ActionTable: actions, Item: item, Intro: "Choose an action", MouseClick: MouseClick);
                    if (action != null && !action.WantsAsync)
                    {
                        Success = action.Process(TradeLine, Vendor, item, TradeLine.context.data.traderInventory ? Vendor : The.Player);
                    }
                });
                if (action.ClearAndSetUpTradeUI)
                {
                    TradeLine.screen.ClearAndSetupTradeUI();
                }
                TradeLine.screen.UpdateViewFromData();
                if (action != null && action.WantsAsync)
                {
                    Success = action.Process(TradeLine, Vendor, item, TradeLine.context.data.traderInventory ? Vendor : The.Player);
                }
            }
        }
    }
}
