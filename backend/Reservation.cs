public class Reservation
{
    public int Id { get; set; }
    public string DogName { get; set; } = "";
    public int? DogId { get; set; }
    public Dog? Dog { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly? ArrivalTime { get; set; }
    public TimeOnly? DepartureTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Occupation> Occupations { get; set; } = [];
}
