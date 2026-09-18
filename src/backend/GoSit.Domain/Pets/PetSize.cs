namespace GoSit.Domain.Pets;

/// <summary>
/// How big an animal is. Optional, and mostly meaningful for dogs — it is the first thing a
/// walker wants to know before accepting.
/// </summary>
public enum PetSize
{
    Small = 1,
    Medium = 2,
    Large = 3,
    ExtraLarge = 4,
}
