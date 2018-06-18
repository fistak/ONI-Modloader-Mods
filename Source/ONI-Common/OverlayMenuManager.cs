using Harmony;
using ONI_Common;
using ONI_Common.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ONI_Common
{
    /// <summary>
    /// Allows for adding buttons to OverlayMenu, together with Hotkeys.
    /// </summary>
    public static class OverlayMenuManager
    {
        // TODO:
        // - test with two seperate libraries, check for duplicating Actions/SimViewModes
        // - there is an odd string in controls
        public static void ScheduleOverlayButton(OverlayRegisterData registerData)
        {
            int freeActionID = GetFirstFreeEnum(typeof(Action), ReservedActionsValues);
            int freeSimViewModeID = GetFirstFreeEnum(typeof(SimViewMode), ReservedSimViewModesValues);

            ReservedActionsValues.Add(freeActionID);
            ReservedSimViewModesValues.Add(freeSimViewModeID);

            var subscriber = new OverlaySubscriber(registerData, (Action)freeActionID, (SimViewMode)freeSimViewModeID);

            Subscribers.Add(subscriber);
        }

        public static List<OverlaySubscriber> Subscribers = new List<OverlaySubscriber>();

        // populate once with startup values instead
        public static List<int> ReservedActionsValues = new List<int>();
        public static List<int> ReservedSimViewModesValues = new List<int>();

        private static int GetFirstFreeEnum(Type enumType, IEnumerable<int> additionalExclusions = null, int startingValue = 0)
        {
            List<int> values = Enum.GetValues(enumType).Cast<int>().ToList();

            if (additionalExclusions != null)
            {
                values.AddRange(additionalExclusions);
            }

            int limit = int.MaxValue - 1;

            for (int current = startingValue; current < limit; current++)
            {
                if (!values.Contains(current))
                {
                    Debug.Log("Found enum: " + current);
                    return current;
                }
            }

            Debug.Log("Can't find free enum value for " + enumType);

            return int.MinValue;
        }
    }

    [HarmonyPatch(typeof(OverlayMenu), "InitializeToggles")]
    public static class OverlayMenu_InitializeToggles
    {
        [HarmonyPostfix]
        public static void Postfix(OverlayMenu __instance, ref List<KIconToggleMenu.ToggleInfo> __result)
        {
            try
            {
                Type overlayToggleInfo = AccessTools.Inner(typeof(OverlayMenu), "OverlayToggleInfo");
                ConstructorInfo ci = overlayToggleInfo.GetConstructor(new Type[] { typeof(string), typeof(string), typeof(SimViewMode), typeof(string), typeof(Action), typeof(string), typeof(string) });

                foreach (var subscriber in OverlayMenuManager.Subscribers)
                {
                    try
                    {
                        object toggleInfo = ci.Invoke(new object[] {
                            subscriber.RegisterData.Name,           // text
                            string.Empty,                           // icon
                            subscriber.SimViewMode,                 // userdata
                            string.Empty,                           // ???
                            subscriber.Action,                      //
                            subscriber.RegisterData.Description,    // tooltip
                            subscriber.RegisterData.Name            // tooltip header
                        });

                        ((KIconToggleMenu.ToggleInfo)toggleInfo).getSpriteCB = GetUISprite;

                        __result.Add((KIconToggleMenu.ToggleInfo)toggleInfo);
                    }
                    catch (Exception e)
                    {
                        State.Logger.Log($"Can't create ToggleInfo for subscriber: {subscriber.RegisterData.Name}");
                        State.Logger.Log(e);
                    }
                }
            }
            catch (Exception e)
            {
                State.Logger.Log($"General error @ OverlayMenuManager.OverlayMenu_InitializeToggles Postfix");
                State.Logger.Log(e);
            }
        }

        private static UnityEngine.Sprite GetUISprite()
        {
            return FileManager.LoadSpriteFromFile(Paths.MaterialColorOverlayIconPath, 256, 256);
        }
    }

    [HarmonyPatch(typeof(Global), "GenerateDefaultBindings")]
    public static class Global_GenerateDefaultBindings
    {
        [HarmonyPostfix]
        public static void Postfix(ref BindingEntry[] __result)
        {
            try
            {
                List<BindingEntry> binds = __result.ToList();

                foreach (var subscriber in OverlayMenuManager.Subscribers)
                {
                    try
                    {
                        BindingEntry entry = new BindingEntry(
                                                              "Root",
                                                              GamepadButton.NumButtons,
                                                              subscriber.RegisterData.KeyCode,
                                                              subscriber.RegisterData.ModifierKey,
                                                              subscriber.Action,
                                                              subscriber.RegisterData.Rebindable,
                                                              true);

                        binds.Add(entry);
                    }
                    catch (Exception e)
                    {
                        State.Logger.Log("Can't create BindingEntry for subscriber: " + subscriber.RegisterData.Name);
                        State.Logger.Log(e);
                    }
                }

                __result = binds.ToArray();
            }
            catch (Exception e)
            {
                State.Logger.Log("Keybindings failed:\n" + e);
                State.Logger.Log(e);
            }
        }
    }

    [HarmonyPatch(typeof(OverlayMenu), "OnToggleSelect")]
    public static class OverlayMenu_OnToggleSelect_MatCol
    {
        [HarmonyPrefix]
        // ReSharper disable once InconsistentNaming
        public static bool EnterToggle(OverlayMenu __instance, KIconToggleMenu.ToggleInfo toggle_info)
        {
            try
            {
                SimViewMode viewMode = (SimViewMode)AccessTools.Field(toggle_info.GetType(), "simView").GetValue(toggle_info);

                foreach (var subscriber in OverlayMenuManager.Subscribers)
                {
                    try
                    {
                        if (viewMode == subscriber.SimViewMode)
                        {
                            subscriber.RegisterData.Callback?.Invoke();
                            return false;
                        }
                    }
                    catch (Exception e)
                    {
                        State.Logger.Log("EnterToggle failed.");
                        State.Logger.Log(e);
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                State.Logger.Log("EnterToggle failed.");
                State.Logger.Log(e);
                return true;
            }
        }
    }

    // TODO: add sprite
    public class OverlayRegisterData
    {
        public OverlayRegisterData(string name, string description, System.Action callback, KKeyCode keyCode, Modifier modifierKey = Modifier.None, bool rebindable = true)
        {
            this.Name = name;
            this.Description = description;
            this.Callback = callback;

            this.KeyCode = keyCode;
            this.ModifierKey = modifierKey;
            this.Rebindable = rebindable;
        }

        public readonly string Name;
        public readonly string Description;
        public readonly System.Action Callback;

        public readonly Modifier ModifierKey;
        public readonly KKeyCode KeyCode;
        public readonly bool Rebindable;
    }

    public class OverlaySubscriber
    {
        public OverlaySubscriber(OverlayRegisterData registerData, Action action, SimViewMode simViewMode)
        {
            this.RegisterData = registerData;
            this.Action = action;
            this.SimViewMode = simViewMode;
        }

        public readonly OverlayRegisterData RegisterData;

        public readonly Action Action;
        public readonly SimViewMode SimViewMode;
    }
}
