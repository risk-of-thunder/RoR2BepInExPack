using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RoR2BepInExPack.UnityEngineHooks;

internal unsafe static class FrankenMonoPrintStackOverflowException
{
    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int FlushInstructionCache(nint handle, void* baseAddr, nuint size);

    internal static void Init()
    {
        //TODO: Update this for 2021 unity mono
        return;

        var currentProcess = Process.GetCurrentProcess();

        var baseAddress = currentProcess
            .Modules
            .Cast<ProcessModule>()
            .Single(m => m.ModuleName == "mono-2.0-bdwgc.dll")
            .BaseAddress;

        // hardcoded offset found with a bit of RE to https://github.com/Unity-Technologies/mono/blob/unity-2019.4-mbe/mono/mini/exceptions-amd64.c#L52
        var restoreStackFunc = *(byte**)(baseAddress + 0x49ee60);

        // making sure we're in the right place by checking the first few instructions
        if (*(ulong*)restoreStackFunc != 0x20_ec_83_48_ec_8b_48_55)
        {
            Log.Warning("Unable to apply SOE fix");
            return;
        }

        // overwrite push rbp with nop
        *restoreStackFunc = 0x90;

        FlushInstructionCache(currentProcess.Handle, restoreStackFunc, 128);

        // now we test
#if DEBUG
        /*
        try
        {
            Recurse();
        }
        catch (StackOverflowException e)
        {
            Log.Info("SOE fix applied");
        }
        */
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Recurse() => Recurse();
}
