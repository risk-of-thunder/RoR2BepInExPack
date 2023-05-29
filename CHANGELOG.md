-   `RoR2BepInExPack` (v1.9.0)

    -   For mod developers: Fix `CharacteracterBody.RemoveOldestTimedBuff` which didn't work if the oldest buff had index 0 in the `body.timedBuffs` array.

-   `RoR2BepInExPack` (v1.8.0 - Shipped in BepInExPack `5.4.2111`)

    -   Fix the difficulty coefficient not being called at the start of a `Run`, causing the cost of chests to be incorrect for the first stage when resetting a run or in multiplayer.

-   `RoR2BepInExPack` (v1.7.0 - Shipped in BepInExPack `5.4.2110`)

    -   Log all hook to the log file, this was previously done by `R2API` but made debugging harder in some cases where `R2API` was either initializing too late or for mods that wasn't depending on `R2API`.
    
    -   Add additional event to `SaferAchievementManager` AchievementAttribute collector for mod creators to run custom logic.

-   `RoR2BepInExPack` (v1.6.0 - Shipped in BepInExPack `5.4.2109`)

    -   Fix a softlock related to Artifact of Metamorphosis with custom survivors that are locked behind custom expansions.
    
    -   Remove an unnecessary vanilla log line whenever expose is applied via the damage type.
    
    -   Fix NonLethal damage still killing when you have 1 max hp.
        
-   `RoR2BepInExPack` (v1.5.0 - Shipped in BepInExPack `5.4.2107`)

    -   Fix another potential crash due to the ConVar change introduced on the previous BepInExPack update.
    
    -   Fix WWise crash for dedicated servers.

-   `RoR2BepInExPack` (v1.4.1 - Shipped in BepInExPack `5.4.2106`)

    -   Fix potential crash due to the ConVar change introduced on the previous BepInExPack update.

-   `RoR2BepInExPack` (v1.4.0 - Shipped in BepInExPack `5.4.2105`)

    -   Mod developers can now simply use `[assembly: HG.Reflection.SearchableAttribute.OptInAttribute]` for adding ConVar to their mods without having to use `R2API.CommandHelper` modules or similar methods.
    
    -   The ProjectileCatalog logs an error if more than 256 projectiles are registered, despite the actual limit being much higher. The console log for that "fake warning" is now gone.

-   `RoR2BepInExPack` (v1.3.0 - Shipped in BepInExPack `5.4.2104`)

    -   Fix Eclipse button not being selectable for controllers.
    
    -   Add some `System.Reflection` safety by hooking `Assembly.GetTypes` and catching all potential `ReflectionTypeLoadException`

-   `RoR2BepInExPack` (v1.2.0 - Shipped in BepInExPack `5.4.2103`)

    -   Fix for DynamicBones log spam.
    
    -   Fix for log spam on some deaths.
        
-   `RoR2BepInExPack` (v1.1.0 - Shipped in BepInExPack `5.4.2100`)

    -   Now contains a mod compatibility fix for when multiple corruption (void items) targets for an item are present, a config is available to determine which gets the new stack:
    
        -   Random -> (Default Option) picks randomly.
        
        -   First -> Oldest Target Picked Up.
        
        -   Last -> Newest Target Picked Up.
        
        -   Rarest -> Rarest Target Picked Up (falls back to Newest on ambiguity).
        
        -   Alternate -> All targets get a turn in acquisition order.

-   `RoR2BepInExPack` (v1.0.2 - Shipped in BepInExPack `5.4.1905`)

    -   Fix achievements not working correctly. For real this time.

-   `RoR2BepInExPack` (v1.0.1 - Shipped in BepInExPack `5.4.1904`)

    -   Fix achievements not working correctly.

-   `RoR2BepInExPack` (v1.0.0 - Shipped in BepInExPack `5.4.1900`)

    -   Detour old Resources.Call to Addressable equivalent.
