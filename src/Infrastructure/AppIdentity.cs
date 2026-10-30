using System.Reflection;

namespace TokenDock
{
    static class AppIdentity
    {
        public const string Name = "TokenDock";
        public static string Version
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            }
        }
    }
}
