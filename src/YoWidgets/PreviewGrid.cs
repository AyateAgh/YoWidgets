using System.Windows.Automation.Peers;
namespace YoWidgets;

// Previews are decorative illustrations; the adjacent named toggle is the control.
// Expose one image rather than duplicate calendar cells and disabled player buttons.
internal sealed class PreviewGrid(string title) : Grid
{
    protected override AutomationPeer OnCreateAutomationPeer() => new PreviewPeer(this, title);
    sealed class PreviewPeer(PreviewGrid owner, string title) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore() => "Предпросмотр: " + title;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}
