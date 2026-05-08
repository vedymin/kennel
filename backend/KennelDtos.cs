public record CreateKennelRequest(string? Name);

public record RenameKennelRequest(string? Name);

public record KennelResponse(int Id, string Name)
{
    public KennelResponse(Domain.Kennel kennel) : this(kennel.Id, kennel.Name)
    { }
}
