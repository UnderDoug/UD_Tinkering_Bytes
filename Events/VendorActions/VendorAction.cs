using ConsoleLib.Console;
using Genkit;
using NAudio.SoundFont;
using Qud.API;
using Qud.UI;
using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;

namespace UD_Tinkering_Bytes
{
    public class VendorAction
    {
        public class Comparer : IComparer<VendorAction>
        {
            public bool priorityFirst;

            public int Compare(VendorAction a, VendorAction b)
            {
                if (!priorityFirst)
                {
                    return SortCompare(a, b);
                }
                return PriorityCompare(a, b);
            }
        }

        public string Name;

        public char Key;

        public string Display;

        public string Command;

        public string PreferToHighlight;

        public int Default;

        public int Priority;

        public int? DramsCost;

        public bool FireOnVendor;

        public bool FireOnItem;

        public bool FireOnPlayer;

        public GameObject FireOn;

        public bool WantsAsync;

        public bool ClearAndSetUpTradeUI;

        public bool Process(TradeLine TradeLine, GameObject Vendor, GameObject Item, GameObject Owner)
        {
            Event @event = Event.New("VendorCommandActivating", nameof(Vendor), Vendor, nameof(Item), Item, nameof(Owner), Owner);
            Owner.FireEvent(@event);
            bool handled = false;
            if (!handled && FireOnVendor)
            {
                Vendor.FireEvent(@event);
                handled = VendorActionEvent.Check(TradeLine, Vendor, Vendor, Item, Owner, Command, DramsCost) || handled;
            }
            if (!handled && FireOnItem)
            {
                Item.FireEvent(@event);
                handled = VendorActionEvent.Check(TradeLine, Item, Vendor, Item, Owner, Command, DramsCost) || handled;
            }
            if (!handled && FireOnPlayer)
            {
                The.Player.FireEvent(@event);
                handled = VendorActionEvent.Check(TradeLine, The.Player, Vendor, Item, Owner, Command, DramsCost) || handled;
            }
            if (!handled && GameObject.Validate(ref FireOn))
            {
                FireOn.FireEvent(@event);
                handled = VendorActionEvent.Check(TradeLine, FireOn, Vendor, Item, Owner, Command, DramsCost) || handled;
            }
            return handled;
        }

        public static VendorAction ShowVendorActionMenu(Dictionary<string, VendorAction> ActionTable, GameObject Item = null, string Intro = null, IComparer<VendorAction> Comparer = null, bool MouseClick = false)
        {
            List<VendorAction> actionsList = new();
            foreach ((string _, VendorAction action) in ActionTable)
            {
                actionsList.Add(action);
            }
            actionsList.Sort(Comparer ??= new VendorAction.Comparer(){ priorityFirst = true });

            Dictionary<char, VendorAction> actionsByHotkey = new(16);

            List<VendorAction> actionsWithoutHotkeys = null;
            StringBuilder SB = null;
            foreach (VendorAction vendorAction in actionsList)
            {
                if (vendorAction.Key != ' ' && !ControlManager.isKeyMapped(vendorAction.Key, new List<string> { "UINav", "Menus" }))
                {
                    if (actionsByHotkey.ContainsKey(vendorAction.Key))
                    {
                        actionsWithoutHotkeys ??= new();
                        actionsWithoutHotkeys.Add(vendorAction);
                    }
                    else
                    {
                        actionsByHotkey.Add(vendorAction.Key, vendorAction);
                        vendorAction.Display = ApplyHotkey(vendorAction.Display, vendorAction.Key, vendorAction.PreferToHighlight, ref SB);
                    }
                }
                else
                {
                    vendorAction.Key = ' ';
                }
            }
            if (actionsWithoutHotkeys != null)
            {
                SB ??= Event.NewStringBuilder();
                foreach (VendorAction vendorAction in actionsWithoutHotkeys)
                {
                    char actionHotkey = char.ToUpper(vendorAction.Key);
                    if (actionHotkey != vendorAction.Key && !actionsByHotkey.ContainsKey(actionHotkey))
                    {
                        if (!ControlManager.isKeyMapped(actionHotkey, new List<string> { "UINav", "Menus" }))
                        {
                            vendorAction.Key = actionHotkey;
                            actionsByHotkey.Add(actionHotkey, vendorAction);
                            vendorAction.Display = ApplyHotkey(ColorUtility.StripFormatting(vendorAction.Display), actionHotkey, vendorAction.PreferToHighlight, ref SB);
                        }
                        continue;
                    }
                    string display = vendorAction.Display;
                    display = ColorUtility.StripFormatting(display);
                    bool foundDynamicKey = false;
                    SB.Clear();
                    int i = 0;
                    for (int length = display.Length; i < length; i++)
                    {
                        char dynamicHotkey = display[i];
                        if (!actionsByHotkey.ContainsKey(dynamicHotkey) && !ControlManager.isKeyMapped(dynamicHotkey, new List<string> { "UINav", "Menus" }))
                        {
                            vendorAction.Key = dynamicHotkey;
                            actionsByHotkey.Add(dynamicHotkey, vendorAction);
                            SB.Append("{{hotkey|").Append(dynamicHotkey).Append("}}")
                                .Append(display, i + 1, length - i - 1);
                            foundDynamicKey = true;
                            break;
                        }
                        SB.Append(dynamicHotkey);
                    }
                    if (!foundDynamicKey)
                    {
                        vendorAction.Key = ' ';
                    }
                    vendorAction.Display = SB.ToString();
                }
                actionsList.Sort(Comparer);
            }
            List<string> options = new();
            List<char> hotkeys = new();
            foreach (VendorAction action in actionsList)
            {
                options.Add(action.Display);
                hotkeys.Add(action.Key);
            }

            int defaultSelected = 0;
            int currentDefault = int.MinValue;
            for (int i = 0; i < actionsList.Count; i++)
            {
                if (actionsList[i].Default > currentDefault)
                {
                    defaultSelected = i;
                    currentDefault = actionsList[i].Default;
                }
            }
            bool isConfused = The.Player.IsConfused;
            Location2D popupLocation = null;
            if (MouseClick)
            {
                // Would love to figure this out.
                // popupLocation = PopupMessage.LOCATION_AT_MOUSE_CURSOR;
            }
            int pickedEntry = Popup.PickOption(
                Intro: Intro ?? (isConfused ? Item.DisplayName : null),
                Options: options.ToArray(),
                Hotkeys: hotkeys.ToArray(),
                Context: isConfused ? null : Item,
                IntroIcon: isConfused ? null : Item.RenderForUI(),
                DefaultSelected: defaultSelected,
                AllowEscape: true,
                RespectOptionNewlines: true,
                CenterIntro: true,
                PopupLocation: popupLocation,
                PopupID: "VendorActionMenu:" + (Item?.IDIfAssigned ?? "(noid)"));

            if (pickedEntry < 0)
            {
                return null;
            }
            return actionsList[pickedEntry];
        }

        private static string ApplyHotkey(string Display, char Key, string Prefer, ref StringBuilder SB)
        {
            if (SB == null)
            {
                SB = Event.NewStringBuilder();
            }
            else
            {
                SB.Clear();
            }
            if (!Display.Contains("{{") && !Display.Contains("&"))
            {
                char hotkey = char.ToLower(Key);
                int preferHotkeyIndex = -1;
                int preferKeyIndex = -1;
                if (!string.IsNullOrEmpty(Prefer))
                {
                    preferKeyIndex = Display.IndexOf(Prefer);
                    if (preferKeyIndex != -1)
                    {
                        preferHotkeyIndex = Prefer.IndexOf(Key);
                        if (preferHotkeyIndex == -1 && hotkey != Key)
                        {
                            preferHotkeyIndex = Prefer.IndexOf(hotkey);
                        }
                    }
                }
                int hotkeyIndex = Display.IndexOf(Key);
                if (preferHotkeyIndex != -1)
                {
                    hotkeyIndex = preferHotkeyIndex + preferKeyIndex;
                }
                if (hotkeyIndex != -1)
                {
                    Display = SB.Append(Display, 0, hotkeyIndex).Append("{{hotkey|").Append(Key)
                        .Append("}}")
                        .Append(Display, hotkeyIndex + 1, Display.Length - hotkeyIndex - 1)
                        .ToString();
                }
            }
            return Display;
        }

        public static int SortCompare(VendorAction a, VendorAction b)
        {
            bool aKeyIsBlank = a.Key == ' ';
            bool bKeyIsBlank = b.Key == ' ';
            if (!aKeyIsBlank && !bKeyIsBlank)
            {
                int keyComparison = char.ToUpper(a.Key).CompareTo(char.ToUpper(b.Key));
                if (keyComparison != 0)
                {
                    return keyComparison;
                }
                if (a.Key != b.Key)
                {
                    return -a.Key.CompareTo(b.Key);
                }
                int defaultComparison = a.Default.CompareTo(b.Default);
                if (defaultComparison != 0)
                {
                    return -defaultComparison;
                }
            }
            else if (aKeyIsBlank || bKeyIsBlank)
            {
                return aKeyIsBlank.CompareTo(bKeyIsBlank);
            }
            return PriorityCompare(a, b);
        }

        public static int PriorityCompare(VendorAction a, VendorAction b)
        {
            int priorityComparison = a.Priority.CompareTo(b.Priority);
            if (priorityComparison != 0)
            {
                return -priorityComparison;
            }
            return -a.Display.CompareTo(b.Display);
        }
    }
}
