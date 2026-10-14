using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Authentication;
using SupportDesk.Application.Features.Authentication;
using SupportDesk.Presentation.Controllers;

namespace SupportDesk.UnitTests.Presentation.Controllers;

public sealed class AuthControllerTests
{
    private static readonly AuthenticatedAgent Agent =
        new(7, "Alex Turner", "alex@example.test");

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndAgent()
    {
        var authentication = new Mock<IAgentAuthenticationService>();
        authentication
            .Setup(service => service.AuthenticateAsync(Agent.Email, "CorrectPassword1!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Agent);
        authentication
            .Setup(service => service.IssueAccessToken(Agent))
            .Returns(new IssuedAccessToken("signed.jwt.token", DateTime.UtcNow.AddHours(1)));
        var controller = new AuthController();

        var result = await controller.Login(
            new LoginRequest(Agent.Email, "CorrectPassword1!"),
            authentication.Object,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("signed.jwt.token", response.AccessToken);
        Assert.Equal(Agent.Id, response.AgentId);
        Assert.Equal(Agent.FullName, response.FullName);
        Assert.Equal(Agent.Email, response.Email);
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_ReturnIdenticalUnauthorizedResponses()
    {
        var authentication = new Mock<IAgentAuthenticationService>();
        authentication
            .Setup(service => service.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticatedAgent?)null);
        var controller = new AuthController();

        var wrongPassword = await controller.Login(
            new LoginRequest(Agent.Email, "WrongPassword1!"),
            authentication.Object,
            CancellationToken.None);
        var unknownEmail = await controller.Login(
            new LoginRequest("unknown@example.test", "WrongPassword1!"),
            authentication.Object,
            CancellationToken.None);

        var first = Assert.IsType<ProblemDetails>(Assert.IsType<UnauthorizedObjectResult>(wrongPassword.Result).Value);
        var second = Assert.IsType<ProblemDetails>(Assert.IsType<UnauthorizedObjectResult>(unknownEmail.Result).Value);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.Title, second.Title);
        Assert.Equal(first.Detail, second.Detail);
    }

    [Fact]
    public async Task Me_UsesAuthenticatedClaimAndReturnsAgent()
    {
        var authentication = new Mock<IAgentAuthenticationService>();
        authentication
            .Setup(service => service.GetActiveAgentAsync(Agent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Agent);
        var controller = ControllerWithAgentId(Agent.Id);

        var result = await controller.Me(authentication.Object, CancellationToken.None);

        var response = Assert.IsType<CurrentAgentDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(Agent.Id, response.Id);
        Assert.Equal(Agent.FullName, response.FullName);
        Assert.Equal(Agent.Email, response.Email);
    }

    [Fact]
    public async Task Me_WithoutAuthenticatedIdentity_ReturnsUnauthorized()
    {
        var authentication = new Mock<IAgentAuthenticationService>();
        var controller = new AuthController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Me(authentication.Object, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
        authentication.Verify(
            service => service.GetActiveAgentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("not-an-email", "password")]
    [InlineData("agent@example.test", "")]
    public async Task LoginRequestValidator_RejectsMissingOrMalformedInput(string email, string password)
    {
        var result = await new LoginRequestValidator().ValidateAsync(new LoginRequest(email, password));

        Assert.False(result.IsValid);
    }

    private static AuthController ControllerWithAgentId(int id)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id.ToString())],
            authenticationType: "Test");
        return new AuthController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }
}
