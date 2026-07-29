namespace Games.Fun.Domain;

public static class BenRules
{
    public const int PrimaryWeight = 47;
    public const int RareWeight = 2;
    public const int TotalWeight = (PrimaryWeight * 2) + (RareWeight * 3);

    public static BenAnimationChoice Select(int draw)
    {
        if (draw is < 0 or >= TotalWeight)
            throw new ArgumentOutOfRangeException(nameof(draw));

        return draw switch
        {
            < PrimaryWeight => new(BenAnimationGroup.Primary, 0),
            < PrimaryWeight * 2 => new(BenAnimationGroup.Primary, 1),
            < (PrimaryWeight * 2) + RareWeight => new(BenAnimationGroup.Rare, 0),
            < (PrimaryWeight * 2) + (RareWeight * 2) => new(BenAnimationGroup.Rare, 1),
            _ => new(BenAnimationGroup.Rare, 2),
        };
    }
}
