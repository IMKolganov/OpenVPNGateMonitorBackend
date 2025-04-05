using Microsoft.Extensions.Logging;
using Moq;
using OpenVPNGateMonitor.Services.EasyRsaServices;
using OpenVPNGateMonitor.Services.EasyRsaServices.Interfaces;

namespace OpenVPNGateMonitor.Tests.Services;

public class EasyRsaExecCommandServiceTests
{
    private readonly Mock<ILogger<IEasyRsaExecCommandService>> _loggerMock = new();
    private readonly Mock<ICommandRunner> _runnerMock = new();

    private EasyRsaExecCommandService CreateService() =>
        new EasyRsaExecCommandService(_loggerMock.Object, _runnerMock.Object);

    [Fact]
    public void ExecuteEasyRsaCommand_ShouldReturnSuccess_WhenExitCodeIsZero()
    {
        _runnerMock.Setup(r => r.RunCommand(It.IsAny<string>()))
            .Returns(("Success", "", 0));

        var service = CreateService();
        var result = service.ExecuteEasyRsaCommand("revoke client1", "/opt/easyrsa");

        Assert.True(result.IsSuccess);
        Assert.Equal("Success", result.Output);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", result.Error);
    }

    [Fact]
    public void ExecuteEasyRsaCommand_ShouldReturnFailure_WhenExitCodeNotZero()
    {
        _runnerMock.Setup(r => r.RunCommand(It.IsAny<string>()))
            .Returns(("", "Some error", 1));

        var service = CreateService();
        var result = service.ExecuteEasyRsaCommand("gen-crl", "/easyrsa");

        Assert.False(result.IsSuccess);
        Assert.Equal("Some error", result.Error);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void ExecuteEasyRsaCommand_ShouldCatchException_AndReturn500()
    {
        _runnerMock.Setup(r => r.RunCommand(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Boom!"));

        var service = CreateService();
        var result = service.ExecuteEasyRsaCommand("revoke client1", "/broken");

        Assert.False(result.IsSuccess);
        Assert.Equal(500, result.ExitCode);
        Assert.Contains("Boom!", result.Error);
    }
}
