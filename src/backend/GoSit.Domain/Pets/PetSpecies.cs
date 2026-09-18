namespace GoSit.Domain.Pets;

/// <summary>
/// The kind of animal. Coarse on purpose: a service is offered for a species, and the
/// distinctions that matter within one — size, temperament, medication — live on the pet.
/// </summary>
public enum PetSpecies
{
    Dog = 1,
    Cat = 2,

    /// <summary>Rabbits, guinea pigs, hamsters and the like.</summary>
    SmallAnimal = 3,

    Bird = 4,

    /// <summary>Reptiles, fish, and anything else. Described in the pet's notes.</summary>
    Other = 5,
}
