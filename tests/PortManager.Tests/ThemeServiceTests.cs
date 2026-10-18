using PortManager.Services;
using Microsoft.Win32;

namespace PortManager.Tests;

[TestClass]
public sealed class ThemeServiceTests
{
    [TestMethod]
    [DataRow(ThemePreference.Light)]
    [DataRow(ThemePreference.Dark)]
    public void Saved_preference_is_loaded_after_restart(ThemePreference preference)
    {
        var preferencesPath = @"Software\PortManager.Tests\" + Guid.NewGuid();
        try
        {
            ThemeService.SavePreference(preferencesPath, preference);
            Assert.AreEqual(preference, ThemeService.LoadPreference(preferencesPath));
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(preferencesPath, throwOnMissingSubKey: false); }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Sepia")]
    [DataRow("7")]
    public void Missing_or_unknown_preference_follows_system(string? storedValue)
    {
        var preferencesPath = @"Software\PortManager.Tests\" + Guid.NewGuid();
        try
        {
            if (storedValue is not null)
            {
                using var key = Registry.CurrentUser.CreateSubKey(preferencesPath);
                key.SetValue(ThemeService.ThemeValueName, storedValue);
            }

            Assert.AreEqual(ThemePreference.System, ThemeService.LoadPreference(preferencesPath));
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(preferencesPath, throwOnMissingSubKey: false); }
    }

    [TestMethod]
    [DataRow(ThemePreference.System, true, true)]
    [DataRow(ThemePreference.System, false, false)]
    [DataRow(ThemePreference.Light, false, true)]
    [DataRow(ThemePreference.Dark, true, false)]
    public void Manual_preference_overrides_system_theme(ThemePreference preference, bool systemIsLight, bool expectedIsLight)
    {
        Assert.AreEqual(expectedIsLight, ThemeService.ResolveIsLight(preference, () => systemIsLight));
    }
}
