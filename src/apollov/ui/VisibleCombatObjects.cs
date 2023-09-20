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

namespace Apollov.UI
{
  [HasOptionFlagUpdate]
  public class VisibleCombatObjects : ObjectFinder.Context
  {
    private static readonly VisibleCombatObjects _instance = new VisibleCombatObjects();
    private static readonly List<GameObject> _noObjects = new List<GameObject>();
    private static readonly DistanceSorter _sorter = new DistanceSorter();
    public VisibleCombatObjects() {
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
    static void Prefix(FrameworkDataElement e)
    {
      if (e is ObjectFinderLine.Data data)
      {
        bool distant = The.Player.DistanceTo(data.go) > 1;
        GameManager.Instance.gameQueue.queueSingletonTask("nearby items twiddle", delegate
        {
          EquipmentAPI.TwiddleObject(data.go, Distant: distant);
        });
      }
    }
  }
}