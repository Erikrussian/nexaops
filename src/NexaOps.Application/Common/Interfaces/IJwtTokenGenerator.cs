using NexaOps.Domain.Entities;

namespace NexaOps.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
