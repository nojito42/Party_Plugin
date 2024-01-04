using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace Party_Plugin;

public class PartyPluginSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    public ListNode PartyMemberType { get; set; } = new ListNode();
    public ListNode PartyMembers { get; set; } = new ListNode();
    public ButtonNode Connect { get; set; } = new ButtonNode();

    public ButtonNode Foo { get; set; } = new ButtonNode();

}