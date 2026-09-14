namespace Shed.Services;

public sealed class IdentityBridgeOptions
{
    public const string Section = "IdentityBridge";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://identity.shuneo.com";
}


