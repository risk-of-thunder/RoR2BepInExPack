using System;
using System.Collections;
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
// on every assembly of the appdomain without handling type loading exceptions.
// Also add convenient action event in the method for easy addition of defs
// Additionally the achievement manager code has been optimized to improve performance:
// * Don't scan MMHOOK generated assemblies or the RoR2BepInExPack assembly; these have lots of types but never have achievements
// * Only scan assemblies that reference RoR2 (so the Unity libraries and similar are ignored)
// * IMPORTANT: Don't yield after every single type. Instead we yield each time a certain number of achievements have been found
// * Don't yield at the end of the method, either
public class SaferAchievementManager
{
    private static Hook _hook;
    private static FieldInfo _achievementManagerOnAchievementsRegisteredFieldInfo;

    /// <summary>
    /// Called for each type that implement <see cref="BaseAchievement"/>,
    /// the code tries to find a <see cref="RegisterAchievementAttribute"/> on the type definition
    /// and the event is invoked with the POTENTIALLY NULL <see cref="RegisterAchievementAttribute"/>,
    /// the <see cref="Type"/> is also provided to the event for the subscriber to inspect.
    /// The use case for this event is mostly for mod creators to run their own logic for determining if the <see cref="RegisterAchievementAttribute"/>
    /// should be ultimately used for creating an Achievement.
    /// </summary>
    public static event Func<Type, RegisterAchievementAttribute, RegisterAchievementAttribute> OnRegisterAchievementAttributeFound;

    /// <summary>
    /// Called once all <see cref="AchievementDef"/> have been created
    /// by the code that iterates over all types that implemented <see cref="BaseAchievement"/>
    /// and <see cref="RegisterAchievementAttribute"/>.
    /// You can add or remove <see cref="AchievementDef"/> with this event.
    /// The use case for this event is mostly for mod creators to run their own code
    /// for adding or removing <see cref="AchievementDef"/> to the game.
    /// </summary>
    public static event Action<List<string>, Dictionary<string, AchievementDef>, List<AchievementDef>> OnCollectAchievementDefs;

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

    // this is the original method with GetTypes and GetCustomAttributes safely wrapped, and:
    // * additional events are added for mod creators
    // * performance optimizations are applied (see comments on this class)
    // orig is not called
    private static IEnumerator SaferCollectAchievementDefs(Dictionary<string, AchievementDef> achievementIdentifierToDef)
    {
        var achievementDefs = new List<AchievementDef>();
        achievementIdentifierToDef.Clear();
        var assemblies = new List<Assembly>();

        Assembly ror2BepInExPackAssembly = typeof(RoR2BepInExPack).Assembly;
        Assembly ror2Assembly = typeof(BaseAchievement).Assembly;
        assemblies.Add(ror2Assembly);
        string ror2AssemblyName = ror2Assembly.GetName().Name;
        foreach (var item in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (item.GetName().Name.StartsWith("MMHOOK_") || item == ror2BepInExPackAssembly)
            {
                continue;
            }
            foreach (var referenceAssemblyName in item.GetReferencedAssemblies())
            {
                if (referenceAssemblyName.Name == ror2AssemblyName)
                {
                    assemblies.Add(item);
                    break;
                }
            }
        }

        int typesProcessedThisFrame = 0;
        foreach (var assembly in assemblies)
        {
            var assemblyTypes = assembly.GetTypes();

            foreach (var type in from _type in assemblyTypes
                                 where _type.IsSubclassOf(typeof(BaseAchievement))
                                 orderby _type.Name
                                 select _type)
            {
                RegisterAchievementAttribute registerAchievementAttribute = null;
                try
                {
                    registerAchievementAttribute = (RegisterAchievementAttribute)type.GetCustomAttributes(false).FirstOrDefault((object v) => v is RegisterAchievementAttribute);

                    if (OnRegisterAchievementAttributeFound != null)
                    {
                        registerAchievementAttribute = OnRegisterAchievementAttributeFound.Invoke(type, registerAchievementAttribute);
                    }
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
                            serverTrackerType = registerAchievementAttribute.serverTrackerType,
                            lunarCoinReward = registerAchievementAttribute.lunarCoinReward
                        };

                        if (unlockableDef && unlockableDef.achievementIcon)
                        {
                            achievementDef.SetAchievedIcon(unlockableDef.achievementIcon);
                        }
                        else
                        {
                            achievementDef.iconPath = "Textures/AchievementIcons/tex" + registerAchievementAttribute.identifier + "Icon";
                            achievementDef.PreloadIcon();
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
                    // yield every once in a while to keep the framerate steady
                    const int MAX_TYPES_PROCESSED_PER_FRAME = 100;
                    if (++typesProcessedThisFrame > MAX_TYPES_PROCESSED_PER_FRAME)
                    {
                        typesProcessedThisFrame = 0;
                        yield return null;
                    }
                }
            }
        }

        OnCollectAchievementDefs?.Invoke(AchievementManager.achievementIdentifiers, achievementIdentifierToDef, achievementDefs);

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
