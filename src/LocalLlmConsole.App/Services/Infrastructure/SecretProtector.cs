
namespace LocalLlmConsole.Services;

public static class SecretProtector
{
    private const string Prefix = "dpapi:v1:";
    // Keep the original entropy value so API keys saved by pre-v1 builds remain decryptable after the app rename.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("LocalLlmConsole:model-api-key:v1");

    public static string ProtectSetting(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (value.StartsWith(Prefix, StringComparison.Ordinal)) return value;
        if (!OperatingSystem.IsWindows()) return value;

        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(value),
            Entropy,
            DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public static string UnprotectSetting(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (!value.StartsWith(Prefix, StringComparison.Ordinal)) return value;
        if (!OperatingSystem.IsWindows()) return "";

        try
        {
            var protectedBytes = Convert.FromBase64String(value[Prefix.Length..]);
            var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "";
        }
    }
}
