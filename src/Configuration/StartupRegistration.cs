using System.Windows.Forms;
using Microsoft.Win32;

namespace TokenDock
{
    static class StartupRegistration
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue("TokenDock") != null;
        }

        public static void SetEnabled(bool enabled)
        {
            if (!enabled && !IsEnabled())
                return;
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled)
                    key.SetValue("TokenDock", "\"" + Application.ExecutablePath + "\" --background");
                else
                    key.DeleteValue("TokenDock", false);
            }
        }

        public static void UpdatePathIfEnabled()
        {
            if (IsEnabled())
                SetEnabled(true);
        }
    }
}
