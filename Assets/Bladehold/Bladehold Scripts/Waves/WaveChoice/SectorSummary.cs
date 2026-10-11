using System.Collections.Generic;

/// <summary>
///     What a combat sector paid out and who fell in it (plan 15), kept by <see cref="GameLoopManager" />
///     for the victory screen. Only wave-card rewards and captain kills are tracked here; kill gold and
///     supply live in <see cref="GameStats" /> / <see cref="RunSession" />.
/// </summary>
public class SectorSummary
{
    public int wavesWon;
    public int wavesFailed;
    public int cardGold;
    public int cardSupply;
    public int goblinBlood;
    public int orcishMetal;
    public int trollHeartHp;
    public int draftPicks;
    public int towerRefund;
    /// <summary>Bonus objectives completed this sector (their gold/supply is folded into cardGold/cardSupply).</summary>
    public int bonusObjectives;
    /// <summary>"Captain Kombusta (Nightmare)" per captain killed this sector.</summary>
    public readonly List<string> captainsDefeated = new List<string>();

    public void RecordReward(WaveCard card, int picks)
    {
        if (card == null) return;
        cardGold += card.gold;
        cardSupply += card.supply;
        draftPicks += picks;
        switch (card.bonusType)
        {
            case WaveBonusType.GoblinBlood: goblinBlood += card.bonusAmount; break;
            case WaveBonusType.OrcishMetal: orcishMetal += card.bonusAmount; break;
            case WaveBonusType.TrollHeart: trollHeartHp += card.bonusAmount; break;
        }
    }

    public void RecordBonus(int gold, int supply)
    {
        bonusObjectives++;
        cardGold += gold;
        cardSupply += supply;
    }
}
