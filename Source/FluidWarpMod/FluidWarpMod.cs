﻿using System.Reflection;
using Harmony;
using UnityEngine;

namespace FluidWarpMod
{   
    internal static class FluidWarpMod_Utils
	{
        public const string CHANNEL_LABEL = "Ch.";

        public const string OFF_LABEL = "(off)";

        private static FieldInfo valveSideScreenTargetValveFI = AccessTools.Field(typeof(ValveSideScreen), "targetValve");

        private static FieldInfo valveValveBaseFI = AccessTools.Field(typeof(Valve), "valveBase");

        public static ConduitType GetConduitType(ValveSideScreen __instance) {			
			ConduitType type = ((ValveBase)valveValveBaseFI.GetValue( valveSideScreenTargetValveFI.GetValue(__instance) )).conduitType;
			return type;
		}
	}

    [HarmonyPatch(typeof(Valve), "OnSpawn")]
    internal static class FluidWarpMod_Valve_OnSpawn
    {
        private static void Postfix(Valve __instance, ValveBase ___valveBase)
        {
            WarpSpaceManager.OnValveChannelChange(___valveBase);
            return;
        }
    }

    [HarmonyPatch(typeof(Valve), "OnCleanUp")]
    internal static class FluidWarpMod_Valve_OnCleanUp
    {
        private static void Postfix(Valve __instance, ValveBase ___valveBase)
        {
            WarpSpaceManager.RemoveProviderValve(___valveBase);
            return;
        }
    }


    [HarmonyPatch(typeof(Valve), "UpdateFlow")]
    internal static class FluidWarpMod_Valve_UpdateFlow
    {
        private static void Postfix(Valve __instance, ValveBase ___valveBase)
        {
            WarpSpaceManager.OnValveChannelChange(___valveBase);
            return;
        }
    }

    [HarmonyPatch(typeof(ValveBase), "ConduitUpdate")]
	internal class FluidWarpMod_ValveBase_ConduitUpdate
	{
        public FluidWarpMod_ValveBase_ConduitUpdate()
        {

        }
        private static bool Prefix(ValveBase __instance, float dt, int ___inputCell, int ___outputCell)
		{
            if (__instance.conduitType != LiquidWarpConfig.CONDUIT_TYPE && __instance.conduitType != GasWarpConfig.CONDUIT_TYPE)
            {
                return true;
            }

            int channelNo = Mathf.RoundToInt(__instance.CurrentFlow * 1000.0f); // simple cast to int sometimes gives invalid result
            Logger.LogFormat(" === ValveBase.ConduitUpdate({0}) Prefix conduitType={1}, inputCell={2}, outputCell={3}, channelNo={4}", dt, __instance.conduitType, ___inputCell, ___outputCell, channelNo);

            if (channelNo == 10000)
            {
                // Channel number is set to MaxFlow (10k), which means WarpGate is disabled
                return false;
            }

            ConduitFlow flowManager = null;
            if (__instance.conduitType == LiquidWarpConfig.CONDUIT_TYPE)
            {
                flowManager = Conduit.GetFlowManager(ConduitType.Liquid);
            }
            else if (__instance.conduitType == GasWarpConfig.CONDUIT_TYPE)
            {
                flowManager = Conduit.GetFlowManager(ConduitType.Gas);
            }

			if (!flowManager.HasConduit(___inputCell) || !flowManager.HasConduit(___outputCell))
			{
                __instance.UpdateAnim();
			}

			if (flowManager.HasConduit(___outputCell) && !flowManager.IsConduitFull(___outputCell))
            {
                WarpSpaceManager.RequestFluidFromChannel(__instance, channelNo);
                __instance.UpdateAnim();
                return false;
            }

            return false;
		}
    }

    [HarmonyPatch(typeof(ValveBase), "UpdateAnim")]
    internal class FluidWarpMod_ValveBase_UpdateAnim
    {
        private static FieldInfo controllerFI = AccessTools.Field(typeof(ValveBase), "controller");
        public static bool Prefix(ValveBase __instance)
        {
            var controller = ((KBatchedAnimController)controllerFI.GetValue(__instance));
            float averageRate = Game.Instance.accumulators.GetAverageRate(__instance.AccumulatorHandle);
            if (averageRate <= 0f)
            {
                controller.Play("off", KAnim.PlayMode.Once, 1f, 0f);
            }
            else
            {
                controller.Play("hi", (averageRate > 0f ? KAnim.PlayMode.Loop : KAnim.PlayMode.Once), 1f, 0f);
            }
            return false;
        }
    }


    [HarmonyPatch(typeof(ValveSideScreen), "OnSpawn")]
    internal class FluidWarpMod_ValveSideScreen_OnSpawn
	{
        private static FieldInfo numberInputFI = AccessTools.Field(typeof(ValveSideScreen), "numberInput");

        private static FieldInfo unitsLabelFI = AccessTools.Field(typeof(ValveSideScreen), "unitsLabel");

        private static void Postfix(ValveSideScreen __instance)
        {
            Logger.LogFormat(" === FluidWarpMod_ValveSideScreen_OnSpawn Postfix === ");

			ConduitType type = FluidWarpMod_Utils.GetConduitType(__instance);			
			if (type == LiquidWarpConfig.CONDUIT_TYPE || type == GasWarpConfig.CONDUIT_TYPE)
			{
                if ((float)((KNumberInputField)numberInputFI.GetValue(__instance)).currentValue == 10f)
                {
                    ((LocText)unitsLabelFI.GetValue(__instance)).text = FluidWarpMod_Utils.OFF_LABEL;
                }
                else
                {
                    ((LocText)unitsLabelFI.GetValue(__instance)).text = FluidWarpMod_Utils.CHANNEL_LABEL;
                }
            }
		}
    }

	[HarmonyPatch(typeof(SideScreenContent), "GetTitle")]
	internal class FluidWarpMod_SideScreenContent_GetTitle
	{
		private static bool Prefix(SideScreenContent __instance, ref string __result)
		{
            Logger.LogFormat(" === FluidWarpMod_SideScreenContent_GetTitle Postfix === ");

			if (!(__instance is ValveSideScreen)) return true;

			ConduitType type = FluidWarpMod_Utils.GetConduitType((ValveSideScreen)__instance);
			if (type == LiquidWarpConfig.CONDUIT_TYPE || type == GasWarpConfig.CONDUIT_TYPE)
			{
				__result = "Channel";
				return false;
			}
			else
			{
				return true;
			}		

		}

	}

	[HarmonyPatch(typeof(ValveSideScreen), "SetTarget")]
	internal static class FluidWarpMod_ValveSideScreen_SetTarget
	{
        private static FieldInfo minFlowLabelFI = AccessTools.Field(typeof(ValveSideScreen), "minFlowLabel");
        private static FieldInfo maxFlowLabelFI = AccessTools.Field(typeof(ValveSideScreen), "maxFlowLabel");
        private static void Postfix(ValveSideScreen __instance)
		{
            Logger.LogFormat(" === FluidWarpMod_ValveSideScreen_SetTarget Postfix === ");
            
			ConduitType type = FluidWarpMod_Utils.GetConduitType(__instance);
			if (type == LiquidWarpConfig.CONDUIT_TYPE || type == GasWarpConfig.CONDUIT_TYPE)
			{
				((LocText)minFlowLabelFI.GetValue(__instance)).text = "-Channel";
				((LocText)maxFlowLabelFI.GetValue(__instance)).text = "+Channel";
			}
		}
	}

    [HarmonyPatch(typeof(ValveSideScreen), "UpdateFlowValue")]
    internal class FluidWarpMod_ValveSideScreen_UpdateFlowValue
    {
        private static FieldInfo unitsLabelFI = AccessTools.Field(typeof(ValveSideScreen), "unitsLabel");

        private static void Postfix(ValveSideScreen __instance, float newValue)
        {
            ConduitType type = FluidWarpMod_Utils.GetConduitType(__instance);
            if ((type == LiquidWarpConfig.CONDUIT_TYPE || type == GasWarpConfig.CONDUIT_TYPE))
            {
                if (newValue == 10f)
                {
                    ((LocText)unitsLabelFI.GetValue(__instance)).text = FluidWarpMod_Utils.OFF_LABEL;
                }
                else
                {
                    ((LocText)unitsLabelFI.GetValue(__instance)).text = FluidWarpMod_Utils.CHANNEL_LABEL;
                }
            }
        }
    }
}
