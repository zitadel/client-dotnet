using Xunit;
using Zitadel.Client;

namespace Zitadel.Client.Test;

public class UnitTest1
{
    [Fact]
    public void TestGreeter()
    {
        // Arrange
        var greeter = new Greeter();

        // Act
        var result = greeter.SayHello();

        // Assert
        Assert.Equal("Hello from Zitadel Client!", result);
    }
}
