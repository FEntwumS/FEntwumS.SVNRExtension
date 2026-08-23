using FEntwumS.SVNRExtension.Sbdp.Constants;

namespace FEntwumS.SVNRExtension.Asm.Entities;

public sealed record SvnrProgram(
    IReadOnlyList<ushort> Words, //liste aus 16bit einträge
    IReadOnlyList<AssembledInstruction> Instructions,
    IReadOnlyList<AssemblyDiagnostic> Diagnostics)
{
    public byte[] ToBinaryImage()
    {
        var image = new byte[SbdpConstants.ImageSize];

        for (var word = 0; word < Words.Count; word++)
        {
            image[word * 2] = (byte)(Words[word] >> 8);
            image[word * 2 + 1] = (byte)(Words[word] & 0xFF);
        }

        return image;
    }
}
