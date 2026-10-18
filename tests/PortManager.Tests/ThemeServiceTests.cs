using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PortManager.Infrastructure;
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

    [TestMethod]
    public void Toggle_switches_visible_theme_and_is_restored_after_restart()
    {
        var preferencesPath = @"Software\PortManager.Tests\" + Guid.NewGuid();
        Exception? capturedException = null;
        var thread = new Thread(() =>
        {
            try
            {
                var resources = new ResourceDictionary();
                var dispatcher = Dispatcher.CurrentDispatcher;

                using (var service = new ThemeService(resources, dispatcher, preferencesPath, systemIsLight: () => true))
                {
                    AssertTheme(resources, isLight: true);
                    service.Toggle();
                    AssertTheme(resources, isLight: false);
                    Assert.AreEqual(ThemePreference.Dark, ThemeService.LoadPreference(preferencesPath));
                }

                var restartedResources = new ResourceDictionary();
                using (var restarted = new ThemeService(restartedResources, dispatcher, preferencesPath, systemIsLight: () => true))
                {
                    AssertTheme(restartedResources, isLight: false);
                    restarted.Toggle();
                    AssertTheme(restartedResources, isLight: true);
                    Assert.AreEqual(ThemePreference.Light, ThemeService.LoadPreference(preferencesPath));
                }
            }
            catch (Exception exception)
            {
                capturedException = exception;
            }
        });

        try
        {
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)), "The STA theme test timed out.");
            Assert.IsNull(capturedException, capturedException?.ToString());
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(preferencesPath, throwOnMissingSubKey: false); }
    }

    private static void AssertTheme(ResourceDictionary resources, bool isLight)
    {
        var background = ((SolidColorBrush)resources["WindowBackgroundBrush"]).Color;
        Assert.AreEqual(isLight ? Color.FromRgb(0xF5, 0xF7, 0xFB) : Color.FromRgb(0x11, 0x13, 0x18), background);
        Assert.AreEqual(isLight ? "E708" : "E706", resources["ThemeToggleGlyph"]);
        Assert.AreEqual(isLight ? Strings.SwitchToDarkTheme : Strings.SwitchToLightTheme, resources["ThemeToggleName"]);
    }
}
