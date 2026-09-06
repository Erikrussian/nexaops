namespace NexaOps.Application.Common.Exceptions;

public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException() : base("Bạn không có quyền thực hiện thao tác này")
    {
    }

    public ForbiddenAccessException(string message) : base(message)
    {
    }
}
