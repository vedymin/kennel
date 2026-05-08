public class Incompatibility
{
    public int Id { get; set; }
    public int DogId1 { get; set; }
    public Dog Dog1 { get; set; } = null!;
    public int DogId2 { get; set; }
    public Dog Dog2 { get; set; } = null!;
}
