namespace Clock;

public partial class Configuration
{
    private void EnsureConfigurationFeatureState()
    {
        NormalizeFavoriteTimeZones();
        AlarmConfigurationService.EnsureInitialized(this);
    }
}
