namespace JIRAToModayAPI.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(string clientId);
    }
}
