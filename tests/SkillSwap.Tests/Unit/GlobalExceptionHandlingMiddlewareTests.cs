using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SkillSwap.API.Middleware;
using SkillSwap.Domain.Exceptions;

namespace SkillSwap.Tests.Unit;

public class GlobalExceptionHandlingMiddlewareTests
{
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "SkillSwap.API";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static async Task<(int StatusCode, JsonDocument Document)> InvokeMiddlewareAsync(
        Exception? exceptionToThrow,
        bool isDevelopment = false)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        RequestDelegate next = _ =>
        {
            if (exceptionToThrow != null)
                throw exceptionToThrow;
            return Task.CompletedTask;
        };

        var env = new FakeHostEnvironment
        {
            EnvironmentName = isDevelopment ? Environments.Development : Environments.Production
        };

        var middleware = new GlobalExceptionHandlingMiddleware(
            next,
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseText = await reader.ReadToEndAsync();
        var document = JsonDocument.Parse(responseText);

        return (context.Response.StatusCode, document);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnauthorizedSessionAccessException_Returns403Forbidden()
    {
        var ex = new UnauthorizedSessionAccessException(42, Guid.NewGuid());

        var (statusCode, doc) = await InvokeMiddlewareAsync(ex);

        Assert.Equal((int)HttpStatusCode.Forbidden, statusCode);
        Assert.Equal("Forbidden", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(403, doc.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_WhenNotFoundException_Returns404NotFound()
    {
        var ex = new NotFoundException("Session", 42);

        var (statusCode, doc) = await InvokeMiddlewareAsync(ex);

        Assert.Equal((int)HttpStatusCode.NotFound, statusCode);
        Assert.Equal("Resource Not Found", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(404, doc.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_WhenUnauthorizedAccessException_Returns401Unauthorized()
    {
        var ex = new UnauthorizedAccessException("Authentication required.");

        var (statusCode, doc) = await InvokeMiddlewareAsync(ex);

        Assert.Equal((int)HttpStatusCode.Unauthorized, statusCode);
        Assert.Equal("Unauthorized", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(401, doc.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_WhenGenericDomainException_Returns400BadRequest()
    {
        var ex = new InsufficientBalanceException(60, 30);

        var (statusCode, doc) = await InvokeMiddlewareAsync(ex);

        Assert.Equal((int)HttpStatusCode.BadRequest, statusCode);
        Assert.Equal("Domain Rule Violation", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_Returns500InternalServerError()
    {
        var ex = new InvalidOperationException("Something blew up unexpectedly.");

        var (statusCode, doc) = await InvokeMiddlewareAsync(ex);

        Assert.Equal((int)HttpStatusCode.InternalServerError, statusCode);
        Assert.Equal("An unexpected error occurred", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(500, doc.RootElement.GetProperty("status").GetInt32());
    }
}
