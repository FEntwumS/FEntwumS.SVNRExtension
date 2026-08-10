using System;
using System.Collections.Generic;
using System.Linq;

using FEntwumS.SVNRExtension.Elf.Dwarf.Constants;

namespace FEntwumS.SVNRExtension.Elf.Dwarf;

/// <summary>
/// Ein Debugging Information Entry (DIE) - der Knoten des DWARF-Baums in
/// <c>.debug_info</c>.
/// </summary>
/// <remarks>
/// Der Kernpunkt dieser Klasse: <see cref="HasChildren"/> wird aus der Kinderliste
/// abgeleitet und ist nicht setzbar. Ein DIE kann daher nicht behaupten, Kinder zu
/// haben, ohne welche zu besitzen - und umgekehrt. Der Serializer leitet daraus ab,
/// wann eine Kinderliste durch einen Null-Eintrag abgeschlossen werden muss.
/// Genau dieser Terminator ist in der Python-Fassung von Hand vergessen worden.
/// </remarks>
internal sealed class Die
{
    private readonly List<DieAttribute> _attributes = new();
    private readonly List<Die> _children = new();

    public Die(DwTag tag)
    {
        Tag = tag;
    }

    public Die(DwTag tag, params DieAttribute[] attributes) : this(tag)
    {
        if (attributes is null) throw new ArgumentNullException(nameof(attributes));
        _attributes.AddRange(attributes);
    }

    /// <summary>Der DWARF-Tag, z. B. <see cref="DwTag.Subprogram"/>.</summary>
    public DwTag Tag { get; }

    public IReadOnlyList<DieAttribute> Attributes => _attributes;

    public IReadOnlyList<Die> Children => _children;

    /// <summary>
    /// Abgeleitet, nicht gesetzt. Bestimmt das has_children-Byte in
    /// <c>.debug_abbrev</c> und ob beim Serialisieren ein Terminator folgt.
    /// </summary>
    public bool HasChildren => _children.Count > 0;

    /// <summary>
    /// Offset dieses DIE ab Beginn der <c>.debug_info</c>-Sektion.
    /// Wird vom Layout-Durchlauf gesetzt, bevor serialisiert wird; vorher 0.
    /// Referenzen (<see cref="DieReference"/>) lesen ihn erst danach aus.
    /// </summary>
    public int Offset { get; set; }

    /// <summary>Haengt ein Attribut an. Gibt dieses DIE zurueck (Fluent-Aufruf).</summary>
    public Die Add(DieAttribute attribute)
    {
        if (attribute is null) throw new ArgumentNullException(nameof(attribute));
        _attributes.Add(attribute);
        return this;
    }

    /// <summary>Haengt ein Kind an. Gibt das <em>Kind</em> zurueck, damit sich Baeume verketten lassen.</summary>
    public Die AddChild(Die child)
    {
        if (child is null) throw new ArgumentNullException(nameof(child));
        _children.Add(child);
        return child;
    }

    /// <summary>Erzeugt eine Referenz auf dieses DIE, z. B. fuer <see cref="DwAt.Type"/>.</summary>
    public DieReference Reference() => new(this);

    /// <summary>
    /// Die Abbreviation-Signatur: Tag, has_children und die Reihenfolge der
    /// (Attribut, Form)-Paare. Zwei DIEs mit gleicher Signatur teilen sich einen
    /// Abbrev-Code, damit <c>.debug_abbrev</c> aus demselben Baum entsteht wie
    /// <c>.debug_info</c> und nicht davon abweichen kann.
    /// </summary>
    public AbbrevSignature Signature =>
        new(Tag, HasChildren, _attributes.Select(a => new AbbrevAttribute(a.At, a.Form)).ToArray());

    /// <summary>Dieses DIE und alle Nachfahren in Dokumentreihenfolge (Preorder).</summary>
    public IEnumerable<Die> SelfAndDescendants()
    {
        yield return this;
        foreach (var child in _children)
        {
            foreach (var descendant in child.SelfAndDescendants())
            {
                yield return descendant;
            }
        }
    }

    public override string ToString() =>
        $"{Tag} @0x{Offset:x} ({_attributes.Count} Attribute, {_children.Count} Kinder)";
}
