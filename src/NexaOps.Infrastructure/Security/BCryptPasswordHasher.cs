using NexaOps.Application.Common.Interfaces;

namespace NexaOps.Infrastructure.Security;

public class BCryptPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        // 10 work factor matches default bcryptjs work factor from NestJS
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 10);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
