using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.UI;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using HarmonyLib;
using XRL.UI.Framework;
using Qud.API;
using ConsoleLib.Console;
using XRL.Messages;

namespace Apollov.UI
{
  [HasOptionFlagUpdate]
  public class VisibleCombatObjects : ObjectFinder.Context
  {
    private static readonly VisibleCombatObjects _instance = new VisibleCombatObjects();
    private static readonly List<GameObject> _noObjects = new List<GameObject>();
    private static readonly DistanceSorter _sorter = new DistanceSorter();
    public VisibleCombatObjects()
    {
      GameManager.Instance.gameQueue.queueSingletonTask("VisibleCombatObjectsInit", () => UpdateItems(The.Core));
    }

    public override void Enable()
    {
      XRLCore.RegisterOnBeginPlayerTurnCallback(UpdateItems);
      UpdateItems(The.Core);
    }

    public override void Disable()
    {
      XRLCore.RemoveOnBeginPlayerTurnCallback(UpdateItems);
      UpdateItems(The.Core);
    }

    public void UpdateItems(XRLCore core)
    {
      var objects = Options.VisibleCombatObjects ? The.Player.CurrentZone.FindObjects(go => go.IsVisible()) : _noObjects;
      finder?.UpdateContext(this, objects);
      SingletonWindowBase<NearbyItemsWindow>.instance?.UpdateGameContext();
    }

    [OptionFlagUpdate]
    public static void UpdateFlags()
    {
      if (ObjectFinder.instance != null)
      {
        try
        {
          if (Options.VisibleCombatObjects)
          {
            ObjectFinder.instance.Add(_instance);
          }
          else
          {
            ObjectFinder.instance.Remove(_instance);
          }
        }
        catch (ArgumentException)
        {
          // adding or removal not required. Well, okay.
        }
        typeof(ObjectFinder).GetField("activeSorter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ObjectFinder.instance, _sorter);
      }
    }

  }

  [HasWishCommand]
  public class FungalCureQueasyFix
  {
    [WishCommand("Apollov.UI.PrintFungalCure")]
    public void PrintFungalCure()
    {
      The.Player.ApplyEffect(new XRL.World.Effects.FungalCureQueasy(100));
    }
  }

  public class DistanceSorter : ObjectFinder.Sorter
  {
    readonly XRLCore.SortObjectBydistanceToPlayer Sorter = new XRLCore.SortObjectBydistanceToPlayer();

    public override int Compare((GameObject go, ObjectFinder.Context context) a, (GameObject go, ObjectFinder.Context context) b)
    {
      var aIsCombat = a.go.IsCombatObject();
      var bIsCombat = b.go.IsCombatObject();
      var aIsTakeable = a.go.Takeable;
      var bIsTakeable = b.go.Takeable;
      if (aIsCombat && !bIsCombat)
      {
        return -1;
      }
      else if (bIsCombat && !aIsCombat)
      {
        return 1;
      }
      else if (aIsTakeable && !bIsTakeable)
      {
        return -1;
      }
      else if (bIsTakeable && !aIsTakeable)
      {
        return 1;
      }
      else
      {
        var result = Sorter.Compare(a.go, b.go);
        return result;
      }
    }
  }


  public static class Options
  {
    public static bool VisibleCombatObjects => GetOption("Apollov_VisibleCombatObjects");
    public static bool GetOption(string name) => XRL.UI.Options.GetOption(name).EqualsNoCase("Yes");
  }

  [HarmonyPatch(typeof(NearbyItemsWindow), nameof(NearbyItemsWindow.OnSelect))]
  public static class Patch_NearbyItemsWindow_OnSelect
  {
    static bool Prefix(FrameworkDataElement e)
    {
      if (e is ObjectFinderLine.Data data)
      {
        bool distant = The.Player.DistanceTo(data.go) > 1;
        GameManager.Instance.gameQueue.queueSingletonTask("nearby items twiddle", delegate
        {
          EquipmentAPI.TwiddleObject(data.go, Distant: distant);
        });
      }
      return false;
    }
  }

  public class Apollov_Commandlistener : IPart
  {
    private static readonly string _mobileTraderCommand = "Apollov.UI.VisibleCombatObjects.MobileTrader";

    public override void Register(GameObject Object)
    {
      Object.RegisterPartEvent(this, _mobileTraderCommand);
      base.Register(Object);
    }

    public override bool FireEvent(XRL.World.Event E)
    {
      if (E.ID == _mobileTraderCommand)
      {
        TradeUI.ShowTradeScreen(GameObject.create("Apollov.UI.MobileTrader"));
      }
      return base.FireEvent(E);
    }
  }

  [PlayerMutator]
  [HasCallAfterGameLoaded]
  public class Apollov_PlayerMutator : IPlayerMutator
  {
    public void mutate(GameObject player)
    {
      player.AddPart<Apollov_Commandlistener>();
    }

    [CallAfterGameLoaded]
    private static void RequirePart()
    {
      GameObject player = XRLCore.Core?.Game?.Player?.Body;
      player?.RequirePart<Apollov_Commandlistener>();
    }
  }


  // This one makes pushing "autoexplore" button work as "wait until healed" if HP is not full.
  [HarmonyPatch(typeof(LegacyKeyMapping), nameof(LegacyKeyMapping.GetNextCommand))]
  public static class RestBeforeExploringSimplePatch
  {
    public static bool Prefix(ref string __result, string[] exclusions = null) 
    {
      __result =  LegacyKeyMapping.MapKeyToCommand(Keyboard.getmeta(false), exclusions);
      if (__result == "CmdAutoExplore" && The.Player.GetStat("Hitpoints").Penalty > 0)
      {
        __result = "CmdWaitUntilHealed";
      }
      MessageQueue.AddPlayerMessage(string.Format("RestBeforeExploringPatch.Prefix: {0}", __result));
      return false;
    }
  }

  // [HarmonyPatch(typeof(ActionManager), nameof(ActionManager.RunSegment))]
  // public static class RestBeforeExploringComplicatedPatch
  // {
  //   static readonly MethodInfo _clearSeeds = AccessTools.Method(typeof(InfluenceMap), nameof(InfluenceMap.ClearSeeds));
  //   static readonly List<CodeInstruction> _injectedInstructions = new List<CodeInstruction>() {
  //     // check for full health
  //     CodeInstruction.Call(typeof(The), "get_ActionManager"),
  //     new CodeInstruction(OpCodes.Dup),
  //   };

  //   static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  //   {
  //     var found = false;
  //     foreach (var instruction in instructions)
  //     {
  //       if (instruction.Calls(_clearSeeds))
  //       {
  //         foreach (var newInstruction in _injectedInstructions)
  //         {
  //           yield return newInstruction;
  //         }
  //         // if not full, execute resting
  //         // else execute what was there
  //         found = true;
  //       }
  //       yield return instruction;
  //     }
  //     if (!found)
  //       throw new Exception("");
  //   }

  //   private static void Foo()
  //   {
  //     var method = typeof(RestBeforeExploringComplicatedPatch).GetMethod(nameof(Bar)).GetMethodBody();
  //     var ilBytes = method.GetILAsByteArray();
  //   }

  //   private static void Bar()
  //   {

  //   }
  // }
}
