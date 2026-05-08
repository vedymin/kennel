public record CreateIncompatibilityRequest(int DogId1, int DogId2);

public record IncompatibilityResponse(int Id, int DogId1, int DogId2)
{
    public IncompatibilityResponse(Incompatibility incompatibility) : this(
        incompatibility.Id,
        incompatibility.DogId1,
        incompatibility.DogId2)
    { }
}
