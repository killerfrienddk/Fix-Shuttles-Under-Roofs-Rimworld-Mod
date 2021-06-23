using System.Collections.Generic;
using System.Linq;
using System;
using RimWorld;
using Verse;
using System.Reflection;
using HugsLib;
using HugsLib.Settings;
using UnityEngine;
using HarmonyLib;
using System.Runtime.CompilerServices;
using RimWorld.Planet;
using RimWorld.QuestGen;

namespace FixShuttlesUnderRoof {
    public class SettingTest : ModBase {
        public override string ModIdentifier {
            get { return "RemoveNoRoofRequirementForShuttles"; }
        }
        private SettingHandle<bool> toggle;
        private SettingHandle<bool> toggle2;
        private SettingHandle<bool> toggle3;
        private SettingHandle<bool> toggle4;
        private SettingHandle<bool> toggle5;
        public override void DefsLoaded() {
            toggle = Settings.GetHandle<bool>(
                "RemoveNoRoofRequirementForShuttles",
                "Remove No Roof Requirement For Shuttles",
                "This option enables the no roof requirement for shuttles launching.",
                true);

            toggle2 = Settings.GetHandle<bool>(
                "RemoveNoRoofRequirementForShipBeacons",
                "Remove No Roof Requirement For Ship Beacons",
                "This option enables the no roof requirement for shuttles landing at ship landing beacons.",
                true);

            toggle3 = Settings.GetHandle<bool>(
                "RemoveNoRoofRequirementForAllItemsToBeBuild",
                "Remove No Roof Requirement For Buildings",
                "This option enables the no roof requirement for buildings.",
                true);

            toggle4 = Settings.GetHandle<bool>(
                "RemoveNoRoofRequirementForAllItemsToBeUsed",
                "Everything can be used under a roof",
                "This option enables that everything can be used under a roof.",
                true);

            toggle5 = Settings.GetHandle<bool>(
                "DropObitalTradeItemsOnRandomSpotFixForCaves",
                "Items from obital trade beacon drops on its sone even with roof.",
                "This option makes the obital trade beacon's droppod's will ignores the roof and will drop the items in it's zone without damaging stuff.",
                true);
        }
    }

    [StaticConstructorOnStartup]
    public static class PatchAll {
        static PatchAll() {
            var harmony = new Harmony("com.killerfriend.FixShuttlesUnderRoof.FixShuttles");
            var assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll(assembly);
        }
    }

    #region CompLaunchable_AnyInGroupIsUnderRoof_Patch
    [HarmonyPatch(typeof(CompLaunchable), "get_AnyInGroupIsUnderRoof")]
    static class CompLaunchable_AnyInGroupIsUnderRoof_Patch {
        static bool Prefix(ref bool __result, CompLaunchable __instance) {
            var settings = HugsLibController.Instance.Settings.GetModSettings("RemoveNoRoofRequirementForShuttles");
            SettingHandle<bool> toggle = settings.GetHandle<bool>("RemoveNoRoofRequirementForShuttles");
            if (toggle.Value) {
                __result = false;
                return false;
            } else {
                List<CompTransporter> transportersInGroup = __instance.TransportersInGroup;
                for (int i = 0; i < transportersInGroup.Count; i++) {
                    if (transportersInGroup[i].parent.Position.Roofed(__instance.parent.Map)) {
                        __result = true;
                        return false;
                    }
                }
                __result = false;
                return false;
            }
        }
    }
    #endregion

    #region ShipLandingArea_RecalculateBlockingThing_Patch
    [HarmonyPatch(typeof(ShipLandingArea), "RecalculateBlockingThing")]
    static class ShipLandingArea_RecalculateBlockingThing_Patch {
        static bool Prefix(ref bool ___blockedByRoof, ref CellRect ___rect, ref Map ___map, ref Thing ___firstBlockingThing) {
            var settings = HugsLibController.Instance.Settings.GetModSettings("RemoveNoRoofRequirementForShuttles");
            SettingHandle<bool> toggle = settings.GetHandle<bool>("RemoveNoRoofRequirementForShipBeacons");

            ___blockedByRoof = false;

            foreach (IntVec3 c in ___rect) {
                if (!toggle.Value) {
                    if (c.Roofed(___map)) {
                        ___blockedByRoof = true;
                        break;
                    }
                }

                List<Thing> thingList = c.GetThingList(___map);
                for (int i = 0; i < thingList.Count; i++) {
                    if (!(thingList[i] is Pawn) && (thingList[i].def.Fillage != FillCategory.None || thingList[i].def.IsEdifice() || thingList[i] is Skyfaller)) {
                        ___firstBlockingThing = thingList[i];
                        return false;
                    }
                }
            }

            ___firstBlockingThing = null;
            return false;
        }
    }
    #endregion

    #region PlaceWorker_NotUnderRoof_Patch 
    [HarmonyPatch]
    class PlaceWorker_NotUnderRoof_Patch {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(PlaceWorker), nameof(PlaceWorker.AllowsPlacing))]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static AcceptanceReport BaseMethodDummy(PlaceWorker_NotUnderRoof instance) { return null; }

        [HarmonyPatch(typeof(PlaceWorker_NotUnderRoof), nameof(PlaceWorker_NotUnderRoof.AllowsPlacing))]
        static bool Prefix(ref BuildableDef checkingDef, ref IntVec3 loc, ref Rot4 rot, ref Map map, ref AcceptanceReport __result, ref PlaceWorker_NotUnderRoof __instance, Thing thingToIgnore = null, Thing thing = null) {
            var settings = HugsLibController.Instance.Settings.GetModSettings("RemoveNoRoofRequirementForShuttles");
            SettingHandle<bool> toggle = settings.GetHandle<bool>("RemoveNoRoofRequirementForAllItemsToBeBuild");
            if (toggle.Value) {
                __result = true;
                return false;
            } else {
                if (!map.roofGrid.Roofed(loc)) {
                    __result = true;
                    return false;
                }
            }

            __result = new AcceptanceReport("MustPlaceUnroofed".Translate());
            return false;
        }
    }
    #endregion

    #region Alert_CannotBeUsedRoofed_UnusableBuildings_Patch
    [HarmonyPatch(typeof(Alert_CannotBeUsedRoofed), "get_UnusableBuildings")]
    static class Alert_CannotBeUsedRoofed_UnusableBuildings_Patch {
        static bool Prefix(ref List<Thing> __result, ref List<Thing> ___unusableBuildingsResult, ref List<ThingDef> ___thingDefsToCheck) {
            var settings = HugsLibController.Instance.Settings.GetModSettings("RemoveNoRoofRequirementForShuttles");
            SettingHandle<bool> toggle = settings.GetHandle<bool>("RemoveNoRoofRequirementForAllItemsToBeUsed");

            ___unusableBuildingsResult.Clear();
            if (___thingDefsToCheck == null) {
                ___thingDefsToCheck = new List<ThingDef>();
                if (!toggle.Value) {
                    foreach (ThingDef allDefsListForReading in DefDatabase<ThingDef>.AllDefsListForReading) {
                        if (allDefsListForReading.canBeUsedUnderRoof) {
                            continue;
                        }
                        ___thingDefsToCheck.Add(allDefsListForReading);
                    }
                }
            }
            List<Map> maps = Find.Maps;
            Faction ofPlayer = Faction.OfPlayer;
            for (int i = 0; i < ___thingDefsToCheck.Count; i++) {
                for (int j = 0; j < maps.Count; j++) {
                    List<Thing> things = maps[j].listerThings.ThingsOfDef(___thingDefsToCheck[i]);
                    for (int k = 0; k < things.Count; k++) {
                        if (things[k].Faction == ofPlayer && RoofUtility.IsAnyCellUnderRoof(things[k])) {
                            ___unusableBuildingsResult.Add(things[k]);
                        }
                    }
                }
            }

            __result = ___unusableBuildingsResult;
            return false;
        }
    }
    #endregion

    #region CompScanner Fix
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(CompScanner), "get_CanUseNow")]
    static class CompScanner_CanUseNow_Patch {
        static bool Prefix(ref bool __result, CompScanner __instance, CompPowerTrader ___powerComp, CompForbiddable ___forbiddable) {
            var settings = HugsLibController.Instance.Settings.GetModSettings("RemoveNoRoofRequirementForShuttles");
            SettingHandle<bool> toggle = settings.GetHandle<bool>("RemoveNoRoofRequirementForAllItemsToBeUsed");

            if (!__instance.parent.Spawned) {
                __result = false;
                return false;
            }
            if (___powerComp != null && !___powerComp.PowerOn) {
                __result = false;
                return false;
            }
            if (toggle.Value) {
                __result = toggle.Value;
            } else {
                if (RoofUtility.IsAnyCellUnderRoof(__instance.parent)) {
                    __result = false;
                    return false;
                }
            }
            if (___forbiddable != null && ___forbiddable.Forbidden) {
                __result = false;
                return false;
            }
            __result = __instance.parent.Faction == Faction.OfPlayer;
            return false;
        }
    }
    #endregion

    #region Obital Trade Beacon Drop Fix
    /*[HarmonyReversePatch]*/
    [HarmonyPatch(typeof(TradeUtility), "SpawnDropPod")]
    static class TradeShip_GiveSoldThingToPlayer_Patch {
        static bool Prefix(ref IntVec3 dropSpot, Map map, Thing t) {
            ActiveDropPodInfo activeDropPodInfo = new ActiveDropPodInfo() {
                SingleContainedThing = t,
                leaveSlag = false
            };
            MakeDropPodAt(dropSpot, map, activeDropPodInfo);
            return false;
        }

        public static void MakeDropPodAt(IntVec3 c, Map map, ActiveDropPodInfo info) {
            ActiveDropPod activeDropPod = (ActiveDropPod)ThingMaker.MakeThing(ThingDefOf.ActiveDropPod, null);
            activeDropPod.Contents = info;
            SpawnSkyfaller(ThingDefOf.DropPodIncoming, activeDropPod, c, map);
            foreach (Thing content in (IEnumerable<Thing>)activeDropPod.Contents.innerContainer) {
                Pawn pawn = content as Pawn;
                Pawn pawn1 = pawn;
                if (pawn == null || !pawn1.IsWorldPawn()) {
                    continue;
                }
                Find.WorldPawns.RemovePawn(pawn1);
                Pawn_PsychicEntropyTracker pawnPsychicEntropyTracker = pawn1.psychicEntropy;
                if (pawnPsychicEntropyTracker != null) {
                    pawnPsychicEntropyTracker.SetInitialPsyfocusLevel();
                }
            }
        }

        public static Skyfaller SpawnSkyfaller(ThingDef skyfaller, Thing innerThing, IntVec3 pos, Map map) {
            return (Skyfaller)GenSpawn.Spawn(MakeSkyfaller(skyfaller, innerThing), pos, map, WipeMode.Vanish);
        }

        public static Skyfaller MakeSkyfaller(ThingDef skyfaller, Thing innerThing) {
            var settings = HugsLibController.Instance.Settings.GetModSettings("RemoveNoRoofRequirementForShuttles");
            SettingHandle<bool> toggle = settings.GetHandle<bool>("DropObitalTradeItemsOnRandomSpotFixForCaves");

            skyfaller.skyfaller.hitRoof = !toggle.Value;
            skyfaller.skyfaller.explosionRadius = toggle.Value ? 0f : 3f;
            Skyfaller skyfaller1 = SkyfallerMaker.MakeSkyfaller(skyfaller);
            if (innerThing != null && !skyfaller1.innerContainer.TryAdd(innerThing, true)) {
                Log.Error(string.Concat("Could not add ", innerThing.ToStringSafe<Thing>(), " to a skyfaller."), false);
                innerThing.Destroy(DestroyMode.Vanish);
            }
            return skyfaller1;
        }

        public static bool AnyAdjacentGoodDropSpot(IntVec3 c, Map map, bool allowFogged, bool canRoofPunch) {
            if (DropCellFinder.IsGoodDropSpot(c + IntVec3.North, map, allowFogged, canRoofPunch, true) || DropCellFinder.IsGoodDropSpot(c + IntVec3.East, map, allowFogged, canRoofPunch, true) || DropCellFinder.IsGoodDropSpot(c + IntVec3.South, map, allowFogged, canRoofPunch, true)) {
                return true;
            }
            return DropCellFinder.IsGoodDropSpot(c + IntVec3.West, map, allowFogged, canRoofPunch, true);
        }
    }

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(DropCellFinder), "TradeDropSpot")]
    static class DropCellFinder_TradeDropSpot_Patch {
        static bool Prefix(Map map, ref IntVec3 __result) { //TradeDropSpot
            var settings = HugsLibController.Instance.Settings.GetModSettings("RemoveNoRoofRequirementForShuttles");
            SettingHandle<bool> toggle = settings.GetHandle<bool>("DropObitalTradeItemsOnRandomSpotFixForCaves");

            IntVec3 position;
            IntVec3 intVec3;
            IEnumerable<Building> buildings =
                from b in map.listerBuildings.allBuildingsColonist
                where b.def.IsCommsConsole
                select b;
            IEnumerable<Building> buildings1 =
                from b in map.listerBuildings.allBuildingsColonist
                where b.def.IsOrbitalTradeBeacon
                select b;
            Building building = buildings1.FirstOrDefault<Building>((Building b) => {
                if (!toggle.Value) {
                    if (map.roofGrid.Roofed(b.Position)) {
                        return false;
                    }
                }
                return AnyAdjacentGoodDropSpot(b.Position, map, toggle.Value, toggle.Value);
            });

            if (building != null) {
                position = building.Position;
                IntVec2? nullable = null;
                if (!DropCellFinder.TryFindDropSpotNear(position, map, out intVec3, toggle.Value, toggle.Value, true, nullable)) {
                    Log.Error(string.Concat("Could find no good TradeDropSpot near dropCenter ", position, ". Using a random standable unfogged cell."), false);
                    intVec3 = CellFinderLoose.RandomCellWith((IntVec3 c) => {
                        if (!c.Standable(map)) {
                            return false;
                        }
                        return !c.Fogged(map);
                    }, map, 1000);
                }
                __result = intVec3;
                return false;
            }
            List<Building> buildings2 = new List<Building>();
            buildings2.AddRange(buildings1);
            buildings2.AddRange(buildings);
            buildings2.RemoveAll((Building b) => {
                CompPowerTrader compPowerTrader = b.TryGetComp<CompPowerTrader>();
                if (compPowerTrader == null) {
                    return false;
                }
                return !compPowerTrader.PowerOn;
            });
            Predicate<IntVec3> predicate = (IntVec3 c) => DropCellFinder.IsGoodDropSpot(c, map, toggle.Value, toggle.Value, true);
            if (!buildings2.Any<Building>()) {
                buildings2.AddRange(map.listerBuildings.allBuildingsColonist);
                buildings2.Shuffle<Building>();
                if (!buildings2.Any<Building>()) {
                    __result = CellFinderLoose.RandomCellWith(predicate, map, 1000);
                    return false;
                }
            }
            int num = 8;
            do {
                for (int i = 0; i < buildings2.Count; i++) {
                    if (CellFinder.TryFindRandomCellNear(buildings2[i].Position, map, num, predicate, out position, -1)) {
                        __result = position;
                        return false;
                    }
                }
                num = Mathf.RoundToInt((float)num * 1.1f);
            }
            while (num <= map.Size.x);
            Log.Error("Failed to generate trade drop center. Giving random.", false);
            __result = CellFinderLoose.RandomCellWith(predicate, map, 1000);
            return false;
        }

        public static bool AnyAdjacentGoodDropSpot(IntVec3 c, Map map, bool allowFogged, bool canRoofPunch) {
            if (DropCellFinder.IsGoodDropSpot(c + IntVec3.North, map, allowFogged, canRoofPunch, true) || DropCellFinder.IsGoodDropSpot(c + IntVec3.East, map, allowFogged, canRoofPunch, true) || DropCellFinder.IsGoodDropSpot(c + IntVec3.South, map, allowFogged, canRoofPunch, true)) {
                return true;
            }
            return DropCellFinder.IsGoodDropSpot(c + IntVec3.West, map, allowFogged, canRoofPunch, true);
        }
    }
    #endregion

    #region QuestNode_SpawnSkyfaller_RunInt_Patch
    [HarmonyPatch(typeof(QuestNode_SpawnSkyfaller), "RunInt")]
    static class QuestNode_SpawnSkyfaller_RunInt_Patch {
        public static int QuestNode_SpawnSkyfallerCount = 1;
        public static bool Prefix(ref QuestNode_SpawnSkyfaller __instance) {
            Slate slate = QuestGen.slate;
            Map map = QuestGen.slate.Get<Map>("map", null, false);
            Skyfaller skyfaller = MakeSkyfaller(__instance.skyfallerDef.GetValue(slate), __instance.innerThings.GetValue(slate));
            QuestPart_SpawnThing questPartSpawnThing = new QuestPart_SpawnThing() {
                thing = skyfaller,
                mapParent = map.Parent
            };
            if (__instance.factionOfForSafeSpot.GetValue(slate) != null) {
                questPartSpawnThing.factionForFindingSpot = __instance.factionOfForSafeSpot.GetValue(slate).Faction;
            }
            if (__instance.cell.GetValue(slate).HasValue) {
                questPartSpawnThing.cell = __instance.cell.GetValue(slate).Value;
            }
            questPartSpawnThing.inSignal = QuestGenUtility.HardcodedSignalWithQuestID(__instance.inSignal.GetValue(slate)) ?? QuestGen.slate.Get<string>("inSignal", null, false);
            questPartSpawnThing.lookForSafeSpot = __instance.lookForSafeSpot.GetValue(slate);
            questPartSpawnThing.tryLandInShipLandingZone = __instance.tryLandInShipLandingZone.GetValue(slate);

            QuestGen.quest.AddPart(questPartSpawnThing);
            return false;
        }

        public static Skyfaller MakeSkyfaller(ThingDef skyfaller, IEnumerable<Thing> things) {
            var settings = HugsLibController.Instance.Settings.GetModSettings("RemoveNoRoofRequirementForShuttles");
            SettingHandle<bool> toggle = settings.GetHandle<bool>("DropObitalTradeItemsOnRandomSpotFixForCaves");

            Skyfaller skyfaller1 = SkyfallerMaker.MakeSkyfaller(skyfaller);
            skyfaller1.def.skyfaller.hitRoof = !toggle.Value;
            skyfaller1.def.skyfaller.explosionRadius = toggle.Value ? 0f : 3f;
            if (things != null) {
                skyfaller1.innerContainer.TryAddRangeOrTransfer(things, false, true);
            }
            return skyfaller1;
        }
    }
    #endregion
}
