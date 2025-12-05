using System;
using System.IO;
using System.Text;

namespace RoR2BepInExPack.GameAssetPaths.Version_1_39_0;

public static class GameAssetPathsSerde
{
    public static void Serialize(string binaryFilePathOutput, string[] paths, string[] guids)
    {
        if (paths.Length != guids.Length)
            throw new ArgumentException("paths and guids arrays must have same length.");

        using var fs = File.Create(binaryFilePathOutput);
        using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);

        bw.Write(paths.Length);

        for (var i = 0; i < paths.Length; i++)
        {
            bw.Write(paths[i]);
            bw.Write(guids[i]);
        }
    }

    public static void Deserialize(
        string binaryFilePathInput,
        out string[] paths,
        out string[] guids)
    {
        using var fs = File.OpenRead(binaryFilePathInput);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

        var count = br.ReadInt32();
        paths = new string[count];
        guids = new string[count];

        for (var i = 0; i < count; i++)
        {
            paths[i] = br.ReadString();
            guids[i] = br.ReadString();
        }
    }
}
