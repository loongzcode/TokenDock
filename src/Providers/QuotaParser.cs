using System;

namespace TokenDock
{
    static class QuotaParser
    {
        public static QuotaWindow Read(object o, string label, string usedKey, string resetKey)
        {
            double? n = Json.Number(Json.Get(o, usedKey));
            if (!n.HasValue || n.Value < 0 || n.Value > 100)
                return null;
            return new QuotaWindow
            {
                Label = label,
                Used = n.Value,
                Resets = Json.Number(Json.Get(o, resetKey))
            };
        }
    }
}
