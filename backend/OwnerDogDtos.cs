public record CreateOwnerRequest(string? Name);

public record OwnerResponse(int Id, string Name)
{
    public OwnerResponse(Owner owner) : this(owner.Id, owner.Name)
    { }
}

public record CreateDogRequest(string? Name, int OwnerId);

public record DogResponse(int Id, string Name, int OwnerId)
{
    public DogResponse(Dog dog) : this(dog.Id, dog.Name, dog.OwnerId)
    { }
}
