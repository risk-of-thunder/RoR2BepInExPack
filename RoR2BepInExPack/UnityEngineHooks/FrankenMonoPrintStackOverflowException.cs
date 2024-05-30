using System;
using System.IO;
using System.Runtime.InteropServices;

namespace RoR2BepInExPack.UnityEngineHooks;

internal class FrankenMonoPrintStackOverflowException
{
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

    private const string DllName = "FrankenMonoPrintStackOverflowException.dll";

    internal static void Init(BepInEx.PluginInfo info)
    {
        var dllPath = Path.Combine(Path.GetDirectoryName(info.Location), DllName);
        if (File.Exists(dllPath))
        {
            LoadLibrary(dllPath);
        }
        else
        {
            Log.Warning($"{DllName} not found.");
        }
    }
}
