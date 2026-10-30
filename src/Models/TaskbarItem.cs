using System;

namespace TokenDock
{
    class TaskbarItem
    {
        public string Key, Label, Value, Tooltip, IconKind;
        public double? Battery;
    }
// All coordinates in this class come from the live taskbar. No top-level overlay fallback.
}
