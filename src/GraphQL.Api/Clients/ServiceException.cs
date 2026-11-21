using System.Net;

namespace GraphQL.Api.Clients;

// Loi khi goi service noi bo; duoc anh xa thanh ma HTTP o gateway
public class ServiceException(string code, string message, HttpStatusCode statusCode, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
    public HttpStatusCode StatusCode { get; } = statusCode;

    public int ToHttpStatus() => StatusCode switch
    {
        HttpStatusCode.NotFound => StatusCodes.Status404NotFound,
        HttpStatusCode.BadRequest => StatusCodes.Status400BadRequest,
        HttpStatusCode.Conflict => StatusCodes.Status409Conflict,
        HttpStatusCode.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
        HttpStatusCode.GatewayTimeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status502BadGateway
    };
}