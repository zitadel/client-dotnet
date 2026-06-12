namespace Zitadel.Client;

/// <summary>
/// Minimal smoke-test surface for the packaged assembly.
/// </summary>
public class Greeter
{
    private readonly string _message = "Hello from Zitadel Client!";

    /// <summary>
    /// Returns the greeting message.
    /// </summary>
    public string SayHello()
    {
        return _message;
    }
}
