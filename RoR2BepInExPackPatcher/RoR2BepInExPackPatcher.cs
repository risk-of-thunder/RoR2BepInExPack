using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;

namespace RoR2BepInExPackPatcher;

public static class RoR2BepInExPackPatcher
{
    internal static BepInEx.Logging.ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("RoR2BepInExPackPatcher");

    public static IEnumerable<string> TargetDLLs { get; } = ["HGUnityUtils.dll", "RoR2.dll"];

    public static void Initialize()
    {
    }

    public static void Patch(AssemblyDefinition ass)
    {
        {
            var typeDefinition = ass.MainModule.GetType("HG.MonoBehaviourManager");
            if (typeDefinition != null)
            {
                var fixedUpdate = typeDefinition.Methods.FirstOrDefault(m => m.Name == "FixedUpdate");
                if (fixedUpdate == null)
                {
                    fixedUpdate = new MethodDefinition("FixedUpdate",
                                                MethodAttributes.Private,
                                                ass.MainModule.ImportReference(typeof(void)));
                    typeDefinition.Methods.Add(fixedUpdate);
                    var il = fixedUpdate.Body.GetILProcessor();
                    il.Append(il.Create(OpCodes.Ret));
                }
            }
        }

        {
            var typeDefinition = ass.MainModule.GetType("RoR2.PlayerCharacterMasterController");
            if (typeDefinition != null)
            {
                var fixedUpdate = typeDefinition.Methods.FirstOrDefault(m => m.Name == "FixedUpdate");
                if (fixedUpdate == null)
                {
                    fixedUpdate = new MethodDefinition("FixedUpdate",
                                                MethodAttributes.Private,
                                                ass.MainModule.ImportReference(typeof(void)));
                    typeDefinition.Methods.Add(fixedUpdate);
                    var il = fixedUpdate.Body.GetILProcessor();
                    il.Append(il.Create(OpCodes.Ret));
                }
            }
        }
    }
}
