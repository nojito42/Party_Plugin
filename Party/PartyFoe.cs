using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory;
using ExileCore;

namespace Party_Plugin.Party;

public class PartyFoe : IPartyPluginInstance
{
    public string FoeName { get; set; }
    public Player Foe { get; set; }
    public Element Element { get; set; } = new Element();
    public Element TPButton { get; set; } = new Element();

    public PartyPlugin I => Core.Current.pluginManager.Plugins.Find(e => e.Name == "Party_Plugin").Plugin as PartyPlugin;

    public override string ToString()
    {
        return $"PlayerName: {FoeName ?? "NULL"}, Data.PlayerEntity.Distance: {Foe.Owner.Distance(Entity.Player).ToString() ?? "Null"}";
    }
}

