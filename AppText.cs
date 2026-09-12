using System.Globalization;
using Microsoft.Win32;

namespace RedragonBatteryTray;

internal static class AppText
{
    private const string SettingsKeyPath = @"Software\RedragonM913BatteryTray";
    private const string LanguageValueName = "Language";

    internal static bool IsArabic
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
            string? preference = key?.GetValue(LanguageValueName) as string;
            if (preference?.Equals("ar", StringComparison.OrdinalIgnoreCase) == true)
                return true;
            if (preference?.Equals("en", StringComparison.OrdinalIgnoreCase) == true)
                return false;
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals(
                "ar", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static void SetLanguage(string language)
    {
        if (language is not ("ar" or "en"))
            throw new ArgumentOutOfRangeException(nameof(language));
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath, true);
        key.SetValue(LanguageValueName, language, RegistryValueKind.String);
    }

    internal static string AppTitle => IsArabic ? "بطارية Redragon M913" : "Redragon M913 Battery";
    internal static string Reading => IsArabic ? "جارٍ قراءة بطارية الفأرة…" : "Reading mouse battery…";
    internal static string RefreshNow => IsArabic ? "تحديث الآن" : "Refresh now";
    internal static string OpenRedragon => IsArabic ? "فتح برنامج Redragon" : "Open Redragon software";
    internal static string StartWithWindows => IsArabic ? "التشغيل مع Windows" : "Start with Windows";
    internal static string About => IsArabic ? "حول الأداة" : "About";
    internal static string Language => "اللغة / Language";
    internal static string Arabic => "العربية";
    internal static string English => "English";
    internal static string Exit => IsArabic ? "خروج" : "Exit";
    internal static string UnavailableStatus => IsArabic
        ? "الفأرة غير متصلة أو في وضع السكون"
        : "Mouse disconnected or sleeping";
    internal static string UnavailableTray => IsArabic
        ? "بطارية Redragon M913: غير متاحة"
        : "Redragon M913 battery: unavailable";
    internal static string ReadingTray => IsArabic
        ? "بطارية Redragon M913: جارٍ القراءة…"
        : "Redragon M913 battery: reading…";
    internal static string ReadFailed => IsArabic ? "تعذر قراءة البطارية الآن." : "Battery reading is unavailable.";
    internal static string MainSoftwareMissing => IsArabic
        ? "برنامج Redragon M913 الأصلي غير موجود."
        : "The original Redragon M913 software was not found.";

    internal static string BatteryStatus(int percent) => IsArabic ? $"البطارية: {percent}%" : $"Battery: {percent}%";
    internal static string TrayPercent(int percent) => IsArabic
        ? $"بطارية فأرة Redragon M913: {percent}%"
        : $"Redragon M913 mouse battery: {percent}%";
    internal static string NotificationPercent(int percent) => IsArabic
        ? $"بطارية الفأرة: {percent}%"
        : $"Mouse battery: {percent}%";

    internal static string AboutText
    {
        get
        {
            string version = Application.ProductVersion;
            return IsArabic
                ? $"الإصدار {version}\nيعرض نسبة بطارية فأرة Redragon M913 في منطقة الإشعارات.\n" +
                  "تتحدث القراءة تلقائيًا كل دقيقة ولا ترسل الأداة أي بيانات عبر الإنترنت.\n\n" +
                  "أداة مستقلة غير رسمية وليست تابعة لشركة Redragon."
                : $"Version {version}\nShows the Redragon M913 battery percentage in the notification area.\n" +
                  "The reading refreshes every minute and the utility sends no data over the internet.\n\n" +
                  "Independent, unofficial utility. Not affiliated with Redragon.";
        }
    }

    internal static MessageBoxOptions MessageOptions => IsArabic
        ? MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading
        : 0;

    internal static RightToLeft MenuDirection => IsArabic ? RightToLeft.Yes : RightToLeft.No;

    internal static string DeviceInterfacesUnavailable => IsArabic
        ? "تم العثور على M913 لكن واجهات البطارية غير متاحة."
        : "M913 was found, but its battery interfaces are unavailable.";
    internal static string ReceiverNotConnected => IsArabic
        ? "مستقبل فأرة Redragon M913 غير متصل."
        : "Redragon M913 receiver is not connected.";
    internal static string BatteryReadFailed => IsArabic
        ? "تعذرت قراءة بطارية M913."
        : "Could not read the M913 battery.";
    internal static string OpenCommandChannelFailed => IsArabic
        ? "تعذر فتح قناة أوامر M913."
        : "Could not open the M913 command channel.";
    internal static string OpenReplyChannelFailed => IsArabic
        ? "تعذر فتح قناة رد M913."
        : "Could not open the M913 reply channel.";
    internal static string CommandNotAcknowledged => IsArabic
        ? "لم تستجب الفأرة لأمر البطارية."
        : "The mouse did not accept the battery command.";
    internal static string UnexpectedReply => IsArabic
        ? "وصل رد غير متوقع من M913."
        : "M913 returned an unexpected reply.";
    internal static string InvalidReplyChecksum => IsArabic
        ? "فشل التحقق من سلامة رد البطارية."
        : "The battery reply checksum is invalid.";
    internal static string ReadReplyFailed => IsArabic
        ? "تعذرت قراءة رد البطارية."
        : "Could not read the battery reply.";
    internal static string ReplyTimedOut => IsArabic
        ? "انتهت مهلة رد الفأرة؛ قد تكون في وضع السكون."
        : "The mouse reply timed out; it may be sleeping.";
    internal static string UnknownBatteryValue(byte raw) => IsArabic
        ? $"قيمة بطارية غير معروفة: {raw}."
        : $"Unknown battery value: {raw}.";
    internal static string InvalidReplyLength(uint length) => IsArabic
        ? $"طول رد البطارية غير صحيح: {length}."
        : $"Invalid battery reply length: {length}.";
}
