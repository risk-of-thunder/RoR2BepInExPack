using HG;
using RoR2.Artifacts;
using RoR2.ContentManagement;

namespace RoR2BepInExPack.VanillaFixes;

// The EliteOnlyArtifactManager static constructor populates its elite arrays using static content fields
// If any mod references EliteOnlyArtifactManager before content packs are loaded, the arrays will be filled with null entries
// Fix: Call the EliteOnlyArtifactManager static constructor again after content packs are loaded
internal static class FixEliteOnlyArtifactManager
{
    internal static void Enable()
    {
        ContentManager.onContentPacksAssigned += OnContentPacksAssigned;
    }

    internal static void Disable()
    {
        ContentManager.onContentPacksAssigned -= OnContentPacksAssigned;
    }

    private static void OnContentPacksAssigned(ReadOnlyArray<ReadOnlyContentPack> readOnlyContentPacks) 
    {
        typeof(EliteOnlyArtifactManager).TypeInitializer.Invoke(null, null);
    }
}
