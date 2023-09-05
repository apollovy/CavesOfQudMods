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
      SingletonWindowBase<NearbyItemsWindow>.instance.UpdateGameContext();
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

  [HarmonyPatch(typeof(NearbyItemsWindow), nameof(NearbyItemsWindow.OnSelect))]
  public static class HarmonyPatcher
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