using OneWare.Essentials.LanguageService;
using OneWare.Essentials.ViewModels;

namespace FEntwumS.SVNRExtension;

public class AsmTypeAssistance : TypeAssistanceBase
{
    public AsmTypeAssistance(IEditor editor) : base(editor)
    {
        LineCommentSequence = ";";
    }
    
    public override bool CanAddBreakPoints => true;
}
