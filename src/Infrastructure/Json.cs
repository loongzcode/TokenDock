using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace TokenDock
{
    static class Json
    {
        public static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = 2097152
            };
        }

        public static object Read(string s)
        {
            return Serializer().DeserializeObject(s);
        }

        public static string Write(object o)
        {
            return Serializer().Serialize(o);
        }

        public static object Get(object o, string k)
        {
            var d = o as IDictionary<string, object>;
            return d != null && d.ContainsKey(k) ? d[k] : null;
        }

        public static object At(object o, params string[] keys)
        {
            foreach (var k in keys)
                o = Get(o, k);
            return o;
        }

        public static string Text(object o)
        {
            return o == null ? "" : Convert.ToString(o, CultureInfo.InvariantCulture);
        }

        public static double? Number(object o)
        {
            double v;
            return o != null && double.TryParse(Text(o), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !double.IsNaN(v) && !double.IsInfinity(v) ? (double? )v : null;
        }

        public static long? Integer(object value)
        {
            long result;
            return value != null && long.TryParse(Text(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result >= 0 ? (long? )result : null;
        }

        public static IEnumerable<object> Items(object o)
        {
            var a = o as IEnumerable;
            if (a == null || o is string || o is IDictionary)
                yield break;
            foreach (object x in a)
                yield return x;
        }

        public static void AtomicWrite(string path, string s)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, s, new UTF8Encoding(false));
            try
            {
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }
    }
}
