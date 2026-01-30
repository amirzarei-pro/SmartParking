namespace SmartParking.Host.Services;

public class AuthSettings
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthService
{
    private readonly AuthSettings _settings;

    public AuthService(IConfiguration configuration)
    {
        _settings = configuration.GetSection("Auth").Get<AuthSettings>() ?? new AuthSettings();
    }

    public bool ValidateCredentials(string username, string password)
    {
        return string.Equals(username, _settings.Username, StringComparison.OrdinalIgnoreCase) 
            && password == _settings.Password;
    }
}
