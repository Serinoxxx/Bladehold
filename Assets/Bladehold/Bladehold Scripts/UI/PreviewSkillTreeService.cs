using System;

/// <summary>
///     Display-only <see cref="ISkillTreeService" /> stub for the class-select screen's "Key Skills" row —
///     it renders <see cref="SkillNodeView" />/<see cref="SkillTooltip" /> for a class that isn't the active
///     one (so there's no real purchase state to report), always showing every node as revealed and
///     available at its level-1 cost. Purchases are always refused; nothing is ever persisted.
/// </summary>
public class PreviewSkillTreeService : ISkillTreeService
{
    public SkillTreeSO Tree { get; }

    public event Action OnTreeChanged
    {
        add { }
        remove { }
    }

    public PreviewSkillTreeService(SkillTreeSO tree)
    {
        Tree = tree;
    }

    public bool IsPurchased(string id) => false;

    public int GetLevel(SkillNode node) => 0;

    public bool IsMaxed(SkillNode node) => false;

    public bool IsRevealed(SkillNode node) => true;

    public bool IsTeased(SkillNode node) => false;

    public int GetCost(SkillNode node) => node != null ? node.CostForLevel(1) : 0;

    public bool CanPurchase(SkillNode node) => true;

    public bool TryPurchase(string id) => false;
}
