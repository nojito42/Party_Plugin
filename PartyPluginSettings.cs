using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Party_Plugin;
public class PartyPluginSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public PartySettings PartySettings { get; set; } = new PartySettings();
    public ButtonNode Connect { get; set; } = new ButtonNode();
    public ButtonNode Foo { get; set; } = new ButtonNode();
}
[Submenu]
public class PartySettings 
{
    
    public ListNode PartyMemberType { get; set; } = new ListNode();
    [IgnoreDataMember]
    public ListNode PartyMembers { get; set; } = new ListNode();
}