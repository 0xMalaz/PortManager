using System.Globalization;

namespace PortManager.Infrastructure;

/// <summary>
/// User-facing text in English and Turkish, chosen by the Windows display language.
/// Technical terms (TCP, port, process, PID) and service names stay in English.
/// </summary>
public static class Strings
{
    internal static bool IsTurkish { get; set; } = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";

    private static string T(string english, string turkish) => IsTurkish ? turkish : english;

    // Tray menu and updates
    public static string CheckForUpdates => T("Check for updates", "Güncellemeleri kontrol et");
    public static string InstallUpdate(string version) => T($"Install update ({version})", $"Güncellemeyi yükle ({version})");
    public static string StartWithWindows => T("Start with Windows", "Windows ile başlat");
    public static string Exit => T("Exit", "Çıkış");
    public static string UpdatesWindowTitle => T("Port Manager updates", "Port Manager güncellemeleri");
    public static string UpdatesUnavailable => T(
        "Updates are unavailable in development or unpackaged builds. Install PortManager-Setup.exe first.",
        "Güncellemeler geliştirme veya paketlenmemiş sürümlerde kullanılamaz. Önce PortManager-Setup.exe'yi kurun.");
    public static string CheckingForUpdates => T("Checking for updates…", "Güncellemeler kontrol ediliyor…");
    public static string UpToDate => T("Port Manager is up to date.", "Port Manager güncel.");
    public static string Downloading(string version) => T($"Downloading {version}…", $"{version} indiriliyor…");
    public static string DownloadingProgress(string version, int percent) =>
        T($"Downloading {version}: {percent}%", $"{version} indiriliyor: %{percent}");
    public static string Downloaded(string version) => T($"Downloaded {version}: 100%", $"{version} indirildi: %100");
    public static string Restarting => T("Restarting to apply the update…", "Güncellemeyi uygulamak için yeniden başlatılıyor…");
    public static string UpdateFailed(string error) => T(
        $"Update failed: {error}\nChoose Check for updates to retry.",
        $"Güncelleme başarısız: {error}\nTekrar denemek için Güncellemeleri kontrol et'i seçin.");
    public static string UpdateAvailable(string version) =>
        T($"Port Manager {version} is available.", $"Port Manager {version} kullanılabilir.");

    // Popup
    public static string RefreshActivePorts => T("Refresh active ports", "Aktif portları yenile");
    public static string SearchPortsOrServices => T("Search ports or services", "Port veya servis ara");
    public static string SearchPlaceholder => T("Port, service or PID...", "Port, servis veya PID...");
    public static string ShowAllListeners => T("Show all listeners", "Tüm portları göster");
    public static string ShowDevListeners => T("Show development listeners", "Geliştirme portlarını göster");
    public static string ShowOtherListeners => T("Show other listeners", "Diğer portları göster");
    public static string SortedByPort => T("Sorted by port", "Porta göre sıralı");
    public static string ActiveHeader => T("ACTIVE", "AKTİF");
    public static string ActiveTcpListeners => T("Active TCP listeners", "Aktif TCP portları");
    public static string KillProcessUsingPortFormat => T("Kill process using port {0}", "{0} portunu kullanan process'i sonlandır");
    public static string ListeningCount(int count) => T($"{count} listening", $"{count} dinlemede");
    public static string AllFilter(int count) => T($"All  {count}", $"Tümü  {count}");
    public static string DevFilter(int count) => $"Dev  {count}";
    public static string OtherFilter(int count) => T($"Other  {count}", $"Diğer  {count}");
    public static string NoListeners => T("No TCP listeners are active.", "Dinlemede olan TCP portu yok.");
    public static string NoSearchMatches => T("No listeners match your search.", "Aramanızla eşleşen port yok.");
    public static string NotUpdatedYet => T("Not updated yet", "Henüz güncellenmedi");
    public static string UpdatedAt(DateTime time) => T($"Updated {time:t}", $"Güncellendi: {time:t}");
    public static string CouldNotReadPorts(string error) => T($"Could not read active ports: {error}", $"Aktif portlar okunamadı: {error}");
    public static string TerminationCancelled => T("Termination cancelled.", "Sonlandırma iptal edildi.");
    public static string CouldNotTerminate(string error) => T($"Could not terminate the process: {error}", $"Process sonlandırılamadı: {error}");
    public static string CouldNotUpdateStartup(string error) =>
        T($"Could not update Windows startup: {error}", $"Windows başlangıç ayarı güncellenemedi: {error}");

    // Listener details
    public static string UnknownAddress => T("Unknown address", "Bilinmeyen adres");
    public static string AllInterfaces => T("All interfaces", "Tüm arayüzler");
    public static string JustNow => T("just now", "az önce");
    public static string MinutesAgo(int minutes) => T($"{minutes}m ago", $"{minutes} dk önce");
    public static string HoursAgo(int hours) => T($"{hours}h ago", $"{hours} sa önce");

    // Dialogs
    public static string ConfirmKill(string processName, int processId, string ports) => T(
        $"Kill {processName} (PID {processId})?{Environment.NewLine}{Environment.NewLine}" +
        $"Listening TCP ports owned by this process: {ports}.{Environment.NewLine}{Environment.NewLine}" +
        "The process and all of its child processes will be forcibly terminated. Unsaved work may be lost.",
        $"{processName} (PID {processId}) sonlandırılsın mı?{Environment.NewLine}{Environment.NewLine}" +
        $"Bu process'e ait dinlemedeki TCP portları: {ports}.{Environment.NewLine}{Environment.NewLine}" +
        "Process ve tüm alt process'leri zorla sonlandırılacak. Kaydedilmemiş çalışmalar kaybolabilir.");
    public static string ConfirmRestartAsAdministrator => T(
        "Windows denied permission to terminate this process. Restart Port Manager as administrator and try again?",
        "Windows bu process'i sonlandırma iznini vermedi. Port Manager yönetici olarak yeniden başlatılıp tekrar denensin mi?");
    public static string ExecutablePathUnavailable => T("The Port Manager executable path is unavailable.", "Port Manager uygulama yolu bulunamadı.");
    public static string CouldNotRestartAsAdministrator(string error) => T(
        $"Port Manager could not restart as administrator: {error}",
        $"Port Manager yönetici olarak yeniden başlatılamadı: {error}");

    // Termination
    public static string ProcessProtected => T("This process is protected.", "Bu process korumalı.");
    public static string PortNoLongerOwned(int port) => T($"Port {port} is no longer owned by that process.", $"{port} portu artık o process'e ait değil.");
    public static string PortChangedBeforeTermination(int port) =>
        T($"Port {port} changed before it could be terminated.", $"{port} portu sonlandırılamadan önce değişti.");
    public static string AccessDenied => T("Windows denied permission to terminate this process.", "Windows bu process'i sonlandırma iznini vermedi.");
    public static string ProcessTreeNotTerminated(string error) =>
        T($"The process tree could not be fully terminated: {error}", $"Process ağacı tamamen sonlandırılamadı: {error}");
    public static string WindowsCouldNotTerminate(string error) =>
        T($"Windows could not terminate the process: {error}", $"Windows process'i sonlandıramadı: {error}");
    public static string ProcessIdentityChanged => T("The process identity changed. The list has been refreshed.", "Process kimliği değişti. Liste yenilendi.");
    public static string PortIsFree(int port) => T($"Port {port} is free.", $"{port} portu boşaldı.");
    public static string AnotherProcess => T("another process", "başka bir process");
    public static string PortStillListening(int port, string owner) =>
        T($"Port {port} is still listening under {owner}.", $"{port} portu hâlâ {owner} altında dinlemede.");
    public static string PortTakenOver(int port, string owner) => T(
        $"The original process exited, but port {port} now belongs to {owner}.",
        $"Asıl process kapandı, ancak {port} portu artık {owner} process'ine ait.");

    // Safety policy
    public static string SystemListener => T("Windows owns this system listener.", "Bu sistem portu Windows'a ait.");
    public static string CannotTerminateSelf => T("Port Manager cannot terminate itself.", "Port Manager kendini sonlandıramaz.");
    public static string ProtectedWindowsProcess => T("This is a protected Windows process.", "Bu korumalı bir Windows process'i.");
    public static string SystemDirectoryReadOnly => T(
        "Processes inside the Windows system directory are read-only.",
        "Windows sistem klasöründeki process'ler sonlandırılamaz.");
}
