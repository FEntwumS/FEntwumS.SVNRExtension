using System;

using FEntwumS.SVNRExtension.Elf.Dwarf.Constants;

namespace FEntwumS.SVNRExtension.Elf.Dwarf;

/// <summary>
/// Ein Attribut eines <see cref="Die"/>: Attributcode, Kodierungsform und Wert.
/// </summary>
/// <remarks>
/// Der Konstruktor ist privat. Instanzen entstehen ausschliesslich ueber die
/// statischen Fabrikmethoden, die Form und Werttyp aneinander koppeln - eine
/// <see cref="DwForm.Strp"/> mit einem String als Wert ist damit nicht konstruierbar.
/// </remarks>
internal sealed class DieAttribute
{
    private DieAttribute(DwAt at, DwForm form, object? value)
    {
        At = at;
        Form = form;
        Value = value;
    }

    public DwAt At { get; }

    public DwForm Form { get; }

    /// <summary>
    /// Je nach <see cref="Form"/>: <see cref="uint"/>, <see cref="string"/>,
    /// <see cref="byte"/>[] (Ausdrucksblock), <see cref="DieReference"/>
    /// oder <c>null</c> bei <see cref="DwForm.FlagPresent"/>.
    /// </summary>
    public object? Value { get; }

    /// <summary>Flag ohne Datenbytes - die reine Anwesenheit im Abbrev ist die Aussage.</summary>
    public static DieAttribute FlagPresent(DwAt at) => new(at, DwForm.FlagPresent, null);

    /// <summary>Ein Byte, z. B. <see cref="DwAt.DeclLine"/> oder <see cref="DwAt.ByteSize"/>.</summary>
    public static DieAttribute Data1(DwAt at, byte value) => new(at, DwForm.Data1, value);

    /// <summary>Vier Bytes Konstante, z. B. <see cref="DwAt.HighPc"/> als Laenge (DWARF 4).</summary>
    public static DieAttribute Data4(DwAt at, uint value) => new(at, DwForm.Data4, value);

    /// <summary>Zieladresse in Pointergroesse, z. B. <see cref="DwAt.LowPc"/>.</summary>
    public static DieAttribute Address(DwAt at, uint value) => new(at, DwForm.Addr, value);

    /// <summary>Offset in eine andere Debug-Sektion, z. B. <see cref="DwAt.StmtList"/> nach .debug_line.</summary>
    public static DieAttribute SecOffset(DwAt at, uint value) => new(at, DwForm.SecOffset, value);

    /// <summary>Verweis in <c>.debug_str</c>. Der Offset kommt aus der StringTable.</summary>
    public static DieAttribute StringRef(DwAt at, uint debugStrOffset) => new(at, DwForm.Strp, debugStrOffset);

    /// <summary>String direkt im DIE, nullterminiert. Fuer kurze, einmalige Namen.</summary>
    public static DieAttribute InlineString(DwAt at, string value) =>
        new(at, DwForm.String, value ?? throw new ArgumentNullException(nameof(value)));

    /// <summary>
    /// Verweis auf ein anderes DIE derselben Compilation Unit, z. B. <see cref="DwAt.Type"/>.
    /// Der Offset wird erst beim Serialisieren aufgeloest - keine handberechneten Konstanten.
    /// </summary>
    public static DieAttribute Reference(DwAt at, Die target) =>
        new(at, DwForm.Ref4, new DieReference(target));

    /// <summary>
    /// DWARF-Ausdruck, z. B. <c>DW_OP_addr &lt;adresse&gt;</c> fuer
    /// <see cref="DwAt.Location"/>. Die Laenge schreibt der Serializer als ULEB128 davor.
    /// </summary>
    public static DieAttribute ExprLoc(DwAt at, params byte[] expression) =>
        new(at, DwForm.ExprLoc, expression ?? throw new ArgumentNullException(nameof(expression)));

    public override string ToString() => $"{At} ({Form}) = {Value}";
}
