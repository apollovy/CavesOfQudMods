using System;
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
  [HasWishCommand]
  public class VisibleCombatObjects : ObjectFinder.Context
  {
    public VisibleCombatObjects() {
      GameManager.Instance.gameQueue.queueSingletonTask("VisibleCombatObjectsInit", () => UpdateItems(The.Core));
    }

    public override void Enable()
    {
      XRLCore.RegisterOnBeginPlayerTurnCallback(new Action<XRLCore>(UpdateItems));
      XRLCore.RegisterOnEndPlayerTurnCallback(new Action<XRLCore>(UpdateItems), true);
    }

    public override void Disable()
    {
      XRLCore.RemoveOnBeginPlayerTurnCallback(new Action<XRLCore>(UpdateItems));
      XRLCore.RemoveOnEndPlayerTurnCallback(new Action<XRLCore>(UpdateItems), true);
    }

    public void UpdateItems(XRLCore core)
    {
      var objects = The.Player.CurrentZone.FindObjects(go => go.IsVisible());
      finder.UpdateContext(this, objects);
      SingletonWindowBase<NearbyItemsWindow>.instance?.UpdateGameContext();
    }

    [WishCommand("Apollov.UI.VisibleCombatObjects")]
    public void Wish()
    {
      ObjectFinder.instance.Add(this);
      typeof(ObjectFinder).GetField("activeSorter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ObjectFinder.instance, new DistanceSorter());
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

  [HarmonyPatch(typeof(XRLGame), nameof(XRLGame.LoadGame))]
  class Patch_XRL_Core_XRLCore
  {
    static void Postfix()
    {
      if (Options.VisibleCombatObjects)
      {
        WishManager.HandleWish("Apollov.UI.VisibleCombatObjects");
      }
    }
  }
}