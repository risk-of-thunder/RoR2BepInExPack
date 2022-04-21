using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.Achievements;
using RoR2BepInExPack.Reflection;
using UnityEngine;

namespace RoR2BepInExPack.VanillaFixes;

// SaferAchievementManager SystemInitializer method use GetTypes
// on every assembly of the appdomain without handling type loading exceptions
internal class SaferAchievementManager
{
    private static Hook _hook;
    private static FieldInfo _achievementManagerOnAchievementsRegisteredFieldInfo;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };

        _hook = new Hook(
                        typeof(AchievementManager).GetMethod(nameof(AchievementManager.CollectAchievementDefs), ReflectionHelper.AllFlags),
                        typeof(SaferAchievementManager).GetMethod(nameof(SaferAchievementManager.SaferCollectAchievementDefs), ReflectionHelper.AllFlags),
                        hookConfig
                    );

        _achievementManagerOnAchievementsRegisteredFieldInfo = typeof(AchievementManager).GetField(nameof(AchievementManager.onAchievementsRegistered), ReflectionHelper.AllFlags);
    }

    internal static void Enable()
    {
        _hook.Apply();
    }

    internal static void Disable()
    {
        _hook.Undo();
    }

    internal static void Destroy()
    {
        _hook.Free();
    }

    // this is the original method 1:1 except GetTypes and GetCustomAttributes is safely wrapped
    // orig is not called
    private static void SaferCollectAchievementDefs(Action<Dictionary<string, AchievementDef>> _, Dictionary<string, AchievementDef> achievementIdentifierToDef)
    {
        var achievementDefs = new List<AchievementDef>();
        achievementIdentifierToDef.Clear();
        var assemblies = new List<Assembly>();

        if (RoR2Application.isModded)
        {
            foreach (var item in AppDomain.CurrentDomain.GetAssemblies())
            {
                assemblies.Add(item);
            }
        }
        else
        {
            assemblies.Add(typeof(BaseAchievement).Assembly);
        }

        foreach (var assembly in assemblies)
        {
            if (ReflectionHelper.GetTypesSafe(assembly, out var assemblyTypes))
            {
                Log.Debug($"{nameof(SaferAchievementManager)} - assembly.GetTypes() failed for : " + assembly.FullName + Environment.NewLine + "Not all types will be scanned.");
            }

            foreach (var type in from _type in assemblyTypes
                                 where _type.IsSubclassOf(typeof(BaseAchievement))
                                 orderby _type.Name
                                 select _type)
            {
                RegisterAchievementAttribute registerAchievementAttribute = null;
                try
                {
                    registerAchievementAttribute = (RegisterAchievementAttribute)type.GetCustomAttributes(false).FirstOrDefault((object v) => v is RegisterAchievementAttribute);
                }
                catch (Exception ex)
                {
                    Log.Debug("RegisterAchievementAttribute type.GetCustomAttributes(false) failed for :  " + type.FullName + Environment.NewLine + ex);
                }

                if (registerAchievementAttribute != null)
                {
                    if (achievementIdentifierToDef.ContainsKey(registerAchievementAttribute.identifier))
                    {
                        Debug.LogErrorFormat("Class {0} attempted to register as achievement {1}, but class {2} has already registered as that achievement.", new object[]
                        {
                                type.FullName,
                                registerAchievementAttribute.identifier,
                                AchievementManager.achievementNamesToDefs[registerAchievementAttribute.identifier].type.FullName
                        });
                    }
                    else
                    {
                        var unlockableDef = UnlockableCatalog.GetUnlockableDef(registerAchievementAttribute.unlockableRewardIdentifier);

                        var achievementDef = new AchievementDef
                        {
                            identifier = registerAchievementAttribute.identifier,
                            unlockableRewardIdentifier = registerAchievementAttribute.unlockableRewardIdentifier,
                            prerequisiteAchievementIdentifier = registerAchievementAttribute.prerequisiteAchievementIdentifier,
                            nameToken = "ACHIEVEMENT_" + registerAchievementAttribute.identifier.ToUpper(CultureInfo.InvariantCulture) + "_NAME",
                            descriptionToken = "ACHIEVEMENT_" + registerAchievementAttribute.identifier.ToUpper(CultureInfo.InvariantCulture) + "_DESCRIPTION",
                            type = type,
                            serverTrackerType = registerAchievementAttribute.serverTrackerType
                        };

                        if (unlockableDef && unlockableDef.achievementIcon)
                        {
                            achievementDef.SetAchievedIcon(unlockableDef.achievementIcon);
                        }
                        else
                        {
                            achievementDef.iconPath = "Textures/AchievementIcons/tex" + registerAchievementAttribute.identifier + "Icon";
                        }

                        AchievementManager.achievementIdentifiers.Add(registerAchievementAttribute.identifier);

                        achievementIdentifierToDef.Add(registerAchievementAttribute.identifier, achievementDef);

                        achievementDefs.Add(achievementDef);

                        if (unlockableDef != null)
                        {
                            unlockableDef.getHowToUnlockString = (() => Language.GetStringFormatted("UNLOCK_VIA_ACHIEVEMENT_FORMAT", new object[]
                            {
                                    Language.GetString(achievementDef.nameToken),
                                    Language.GetString(achievementDef.descriptionToken)
                            }));
                            unlockableDef.getUnlockedString = (() => Language.GetStringFormatted("UNLOCKED_FORMAT", new object[]
                            {
                                    Language.GetString(achievementDef.nameToken),
                                    Language.GetString(achievementDef.descriptionToken)
                            }));
                        }
                    }
                }
            }
        }

        AchievementManager.achievementDefs = achievementDefs.ToArray();
        AchievementManager.SortAchievements(AchievementManager.achievementDefs);
        AchievementManager.serverAchievementDefs = (from achievementDef in AchievementManager.achievementDefs
                                                    where achievementDef.serverTrackerType != null
                                                    select achievementDef).ToArray();

        for (var j = 0; j < AchievementManager.achievementDefs.Length; j++)
        {
            AchievementManager.achievementDefs[j].index = new AchievementIndex
            {
                intValue = j
            };
        }

        for (var k = 0; k < AchievementManager.serverAchievementDefs.Length; k++)
        {
            AchievementManager.serverAchievementDefs[k].serverIndex = new ServerAchievementIndex
            {
                intValue = k
            };
        }

        for (var l = 0; l < AchievementManager.achievementIdentifiers.Count; l++)
        {
            var currentAchievementIdentifier = AchievementManager.achievementIdentifiers[l];

            achievementIdentifierToDef[currentAchievementIdentifier].childAchievementIdentifiers =
                (from v in AchievementManager.achievementIdentifiers
                 where achievementIdentifierToDef[v].prerequisiteAchievementIdentifier == currentAchievementIdentifier
                 select v).ToArray();
        }

        var onAchievementsRegistered = (Action)_achievementManagerOnAchievementsRegisteredFieldInfo.GetValue(null);
        onAchievementsRegistered?.Invoke();
    }
}
