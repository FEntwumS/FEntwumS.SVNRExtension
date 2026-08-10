using System;

namespace FEntwumS.SVNRExtension.Elf.Dwarf;

/// <summary>
/// Symbolischer Verweis auf ein anderes <see cref="Die"/>.
/// </summary>
/// <remarks>
/// Ersetzt handberechnete Konstanten wie <c>base_type_offset = 0x3f</c>. Der
/// tatsaechliche Offset wird erst gelesen, nachdem der Layout-Durchlauf allen DIEs
/// ihre <see cref="Die.Offset"/>-Werte zugewiesen hat. Da alle Referenzformen
/// (<see cref="DwForm.Ref4"/>) feste 4 Byte belegen, haengen die Groessen nicht von
/// den Zielwerten ab - ein einziger Layout-Durchlauf genuegt.
/// </remarks>
internal sealed class DieReference
{
    public DieReference(Die target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public Die Target { get; }

    /// <summary>Offset des Ziels ab Beginn von <c>.debug_info</c>.</summary>
    public int Offset => Target.Offset;

    public override string ToString() => $"-> 0x{Offset:x} ({Target.Tag})";
}
