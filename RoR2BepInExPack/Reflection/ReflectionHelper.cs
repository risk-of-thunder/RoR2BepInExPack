using System;
using System.Linq;
using System.Reflection;

namespace RoR2BepInExPack.Reflection;

internal class ReflectionHelper
{
    internal const BindingFlags AllFlags = (BindingFlags)(-1);

    /// <summary>
    ///
    /// </summary>
    /// <param name="assembly"></param>
    /// <param name="assemblyTypes"></param>
    /// <returns>true if a ReflectionTypeLoadException was caught</returns>
    internal static bool GetTypesSafe(Assembly assembly, out Type[] assemblyTypes)
    {
        try
        {
            assemblyTypes = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException re)
        {
            assemblyTypes = re.Types.Where(t => t != null).ToArray();
            return true;
        }

        return false;
    }
}
