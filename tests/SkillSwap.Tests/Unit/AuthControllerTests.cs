using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Auth;

namespace SkillSwap.Tests.Unit;

public class AuthControllerTests
{
    private class FakeAuthService : IAuthService
    {
        public Func<RegisterRequestDto, Task<Result<AuthResponseDto>>>? OnRegister { get; set; }
        public Func<LoginRequestDto, Task<Result<AuthResponseDto>>>? OnLogin { get; set; }

        public Task<Result<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
        {
            if (OnRegister != null)
                return OnRegister(request);

            return Task.FromResult(Result.Success(new AuthResponseDto
            {
                UserId = Guid.NewGuid(),
                Email = request.Email,
                AccessToken = "fake_jwt",
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
            }));
        }

        public Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
        {
            if (OnLogin != null)
                return OnLogin(request);

            return Task.FromResult(Result.Success(new AuthResponseDto
            {
                UserId = Guid.NewGuid(),
                Email = request.Email,
                AccessToken = "fake_jwt",
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
            }));
        }
    }

    [Fact]
    public async Task Register_WhenSuccessful_Returns201Created_WithAuthResponse()
    {
        var fakeService = new FakeAuthService();
        var controller = new AuthController(fakeService);
        var request = new RegisterRequestDto
        {
            Email = "test@example.com",
            Password = "Password123!",
            DisplayName = "Test User"
        };

        var response = await controller.Register(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        var dto = Assert.IsType<AuthResponseDto>(objectResult.Value);
        Assert.Equal("test@example.com", dto.Email);
    }

    [Fact]
    public async Task Register_WhenServiceFails_Returns400BadRequest_WithErrorMessage()
    {
        var fakeService = new FakeAuthService
        {
            OnRegister = _ => Task.FromResult(Result.Failure<AuthResponseDto>("A user with this email already exists."))
        };
        var controller = new AuthController(fakeService);
        var request = new RegisterRequestDto
        {
            Email = "existing@example.com",
            Password = "Password123!",
            DisplayName = "Existing User"
        };

        var response = await controller.Register(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        Assert.Equal("A user with this email already exists.", badRequestResult.Value);
    }

    [Fact]
    public async Task Login_WhenSuccessful_Returns200OK_WithAuthResponse()
    {
        var fakeService = new FakeAuthService();
        var controller = new AuthController(fakeService);
        var request = new LoginRequestDto
        {
            Email = "test@example.com",
            Password = "Password123!"
        };

        var response = await controller.Login(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var dto = Assert.IsType<AuthResponseDto>(okResult.Value);
        Assert.Equal("test@example.com", dto.Email);
    }

    [Fact]
    public async Task Login_WhenServiceFails_Returns400BadRequest_WithErrorMessage()
    {
        var fakeService = new FakeAuthService
        {
            OnLogin = _ => Task.FromResult(Result.Failure<AuthResponseDto>("Invalid email or password."))
        };
        var controller = new AuthController(fakeService);
        var request = new LoginRequestDto
        {
            Email = "test@example.com",
            Password = "WrongPassword!"
        };

        var response = await controller.Login(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        Assert.Equal("Invalid email or password.", badRequestResult.Value);
    }
}
