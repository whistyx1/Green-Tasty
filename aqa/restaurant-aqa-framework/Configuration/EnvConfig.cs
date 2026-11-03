using dotenv.net;

namespace Restaurant_Aqa_Framework.Configuration;

/// <summary>
/// Loads configuration from the current AQA working directory `.env` once per process.
/// Never probes the repository or parent folders for unrelated application secrets. Real environment
/// variables always win over `.env` values (overwriteExistingVars: false), so
/// CI can run with no `.env` file at all and still work.
/// </summary>
public static class EnvConfig
{
    private static readonly Lazy<bool> Loaded = new(() =>
    {
        DotEnv.Load(options: new DotEnvOptions(
            ignoreExceptions: true,
            probeForEnv: false,
            probeLevelsToSearch: 0,
            overwriteExistingVars: false,
            trimValues: true));
        return true;
    });

    public static void EnsureLoaded() => _ = Loaded.Value;
}
