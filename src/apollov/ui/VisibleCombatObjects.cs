using System;
using System.Linq;
using System.Reflection;
using Qud.UI;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.Wish;
using XRL.World;

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
      var combatObjects = The.Player.GetVisibleCombatObjects()
        .Where((GameObject go) => go.IsVisible());
      finder.UpdateContext(this, combatObjects);
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
            var result = Sorter.Compare(a.go, b.go);
            return result;
        }
    }
}