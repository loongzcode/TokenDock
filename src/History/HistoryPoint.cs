using System;

namespace TokenDock
{
    sealed class HistoryPoint
    {
        public int Version { get; set; }
        public double Time { get; set; }
        public string Series { get; set; }
        public string Label { get; set; }
        public string Unit { get; set; }
        public decimal Value { get; set; }
        public double? Reset { get; set; }

        public HistoryPoint()
        {
            Version = 1;
        }

        [System.Web.Script.Serialization.ScriptIgnore]
        public bool Valid
        {
            get
            {
                return Version == 1 && !double.IsNaN(Time) && !double.IsInfinity(Time) && Time > 0 && Time < 253402214400d && !string.IsNullOrWhiteSpace(Series) && Series.Length <= 512 && !string.IsNullOrWhiteSpace(Label) && Label.Length <= 256 && (Unit == "%" || Unit == "CNY" || Unit == "USD") && Value >= 0 && (Unit != "%" || Value <= 100) && (!Reset.HasValue || (!double.IsNaN(Reset.Value) && !double.IsInfinity(Reset.Value)));
            }
        }
    }
}
