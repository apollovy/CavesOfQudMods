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
using System.Reflection.Emit;
using Genkit;
using XRL.World.Capabilities;
using System.Linq;

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


  [HarmonyPatch(typeof(ActionManager), nameof(ActionManager.RunSegment))]
  public static class RestBeforeExploringPatch
  {
    static readonly MethodInfo _clearSeeds = AccessTools.Method(typeof(InfluenceMap), nameof(InfluenceMap.ClearSeeds));

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
      var found = false;
      Label? label = null;
      var instructionsArray = instructions.ToArray();
      for (var i = 0; i < instructionsArray.Length; i++)
      {
        var instruction = instructionsArray[i];
        // // [1377 23 - 1377 65]
        // IL_1834: ldsfld       class [System]System.Diagnostics.Stopwatch XRL.Core.ActionManager::AutomoveTimer
        // IL_1839: callvirt     instance bool [System]System.Diagnostics.Stopwatch::get_IsRunning()
        // IL_183e: brfalse      IL_19b8
        if (instruction.opcode == OpCodes.Ldsfld
          && instruction.operand.ToString() == "System.Diagnostics.Stopwatch AutomoveTimer")
        {
          var nextInstruction = instructionsArray[i + 1];
          if (nextInstruction.opcode == OpCodes.Callvirt
            && nextInstruction.operand.ToString() == "Boolean get_IsRunning()"
            && instructionsArray[i + 2].opcode == OpCodes.Brfalse)
          {
            label = generator.DefineLabel();
            instruction.labels.Add((Label)label);
            break;
          }
        }
      }
      for (var i = 0; i < instructionsArray.Length; i++)
      {
        var instruction = instructionsArray[i];
        if (instruction.Calls(_clearSeeds))
        {
          yield return CodeInstruction.Call(typeof(RestBeforeExploringPatch), nameof(WaitUntilHealed));
          yield return new CodeInstruction(OpCodes.Brtrue, label);
          found = true;
        }
        yield return instruction;
      }
      if (!found)
        throw new Exception("RestBeforeExploringComplicatedPatch was not applied.");
    }
    static bool WaitUntilHealed()
    {
      if (The.Player.GetStat("Hitpoints").Penalty > 0)
      {
        ++The.ActionManager.RestingUntilHealedCount;
        Loading.SetLoadingStatus("Resting until healed... Turn: " + The.ActionManager.RestingUntilHealedCount.ToString());
        The.Player.UseEnergy(1000, "Pass");
        if (The.Player.GetStat("Hitpoints").Penalty <= 0)
        {
          AutoAct.Interrupt();
        }
        else if (The.ActionManager.RestingUntilHealedCount % 10 == 0)
        {
          XRLCore.TenPlayerTurnsPassed();
          The.Core.RenderBase(false, true);
        }
        return true;
      }
      return false;
    }
  }
}
