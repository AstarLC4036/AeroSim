using AeroSim.Cockpit.MFD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AeroSim.Utils
{
    public static class MemoryUtils
    {
        public static T Copy<T>(T src) where T : class
        {
            if (src == null) return null;
            object dst = Activator.CreateInstance(src.GetType());
            foreach (FieldInfo f in src.GetType().GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.IsStatic || f.IsInitOnly) continue;
                f.SetValue(dst, f.GetValue(src));     // including '[SerializeField] private'
            }
            return (T)dst;
        }
    }
}
