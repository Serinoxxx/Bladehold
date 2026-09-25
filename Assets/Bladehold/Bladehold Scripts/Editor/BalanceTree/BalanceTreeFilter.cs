using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     Which Balance Tree nodes are visible. Cards are filtered directly: weapon and element are a union
///     ("sword + fire" = every sword card plus every fire card, duos needing fire included), category, ultimate,
///     demo and search narrow on top. Every other node shows when it hangs off a visible card (or, for armour,
///     off a visible stat), or when it matches the search itself. With no filter set, everything shows.
/// </summary>
[Serializable]
public class BalanceTreeFilter
{
    public string weapon = "";
    public string element = "";
    public string category = "";
    public string search = "";
    public bool ultimatesOnly;
    public bool demoOnly;
    public List<BalanceNodeKind> hiddenKinds = new List<BalanceNodeKind>();

    public bool NarrowsCards =>
        weapon.Length > 0 || element.Length > 0 || category.Length > 0 || ultimatesOnly || search.Trim().Length > 0;

    public HashSet<BalanceNode> Apply(BalanceTreeModel model)
    {
        var visible = new HashSet<BalanceNode>();
        string query = search.Trim().ToLowerInvariant();

        foreach (BalanceNode card in model.Cards)
        {
            if (CardPasses(card, query)) visible.Add(card);
        }

        if (!NarrowsCards)
        {
            foreach (BalanceNode n in model.Nodes)
            {
                if (!demoOnly || !n.DemoLocked) visible.Add(n);
            }
        }
        else
        {
            // Hubs, towers and stats attached to a visible card.
            foreach (BalanceEdge e in model.Edges)
            {
                if (visible.Contains(e.To) && e.To.Kind == BalanceNodeKind.Card) visible.Add(e.From);
                if (visible.Contains(e.From) && e.From.Kind == BalanceNodeKind.Card) visible.Add(e.To);
            }
            // Armour that touches one of those stats.
            foreach (BalanceEdge e in model.Edges)
            {
                if (e.From.Kind == BalanceNodeKind.Armour && visible.Contains(e.To)) visible.Add(e.From);
            }
            if (query.Length > 0)
            {
                foreach (BalanceNode n in model.Nodes)
                {
                    if (n.Kind != BalanceNodeKind.Card && n.SearchText.Contains(query)) visible.Add(n);
                }
            }
            if (demoOnly) visible.RemoveWhere(n => n.DemoLocked);
        }

        visible.RemoveWhere(n => hiddenKinds.Contains(n.Kind));
        return visible;
    }

    private bool CardPasses(BalanceNode card, string query)
    {
        if (demoOnly && card.DemoLocked) return false;
        if (category.Length > 0 && !card.Category.Equals(category, StringComparison.OrdinalIgnoreCase)) return false;
        if (ultimatesOnly && !card.IsUltimate) return false;
        if (query.Length > 0 && !card.SearchText.Contains(query)) return false;

        bool byWeapon = weapon.Length > 0;
        bool byElement = element.Length > 0;
        if (!byWeapon && !byElement) return true;
        return (byWeapon && card.Weapon.Equals(weapon, StringComparison.OrdinalIgnoreCase))
               || (byElement && card.Elements.Contains(element));
    }
}
