public class GoogleConnection
{
    public int Id { get; set; }
    public string EncryptedRefreshToken { get; set; } = "";
    public DateTime ConnectedAt { get; set; }
}
