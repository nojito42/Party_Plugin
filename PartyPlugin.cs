using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ExileCore.Shared.Nodes;
using Party_Plugin.Helpers;
using Party_Plugin.Party;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Threading;
using Party_Plugin.Myserver;
namespace Party_Plugin;



public interface IPartyPluginInstance
{
    public PartyPlugin I { get; }
}

public class PartyPlugin : BaseSettingsPlugin<PartyPluginSettings>
{
    private bool isInParty = false;
    private Party.Party Party = new();
    private MyServer MyServer;
    private MyClient Client;
    public Camera Cam => GameController.IngameState.Camera;
    public Element PartyUI => GameController.IngameState.IngameUi.PartyElement;




    public override bool Initialise()
    {
        Settings.PartyMemberType.Values.AddRange(new string[]{
            "Follower","Leader"
        });

        Settings.Connect.OnPressed += delegate
        {
            if (Settings.PartyMemberType.Value == "Leader")
            {
                if (MyServer == null)
                {
                    MyServer = new MyServer();
                    MyServer.StartServer();
                }
            }
            else
            {
                if (Client == null)
                {
                    Client = new MyClient();
                    Client.StartClient();
                }
                else
                {
                    if (!Client.IsClientRunning)
                        Client.StartClient();
                }
            }
        };
        Settings.Foo.OnPressed += async delegate
        {
            if (Settings.PartyMemberType.Value == "Leader")
            {
                MyServer.BroadcastMessage("coucou");
                LogMsg(MyServer.connectedClients.Count.ToString());
            }
            else
            {
                if (Client == null)
                {
                    Client = new MyClient();
                    Client.StartClient();
                }
                if (!Client.IsClientRunning)
                    Client.StartClient();

                if (Client.IsClientRunning)
                    await Client.SendMessageToServer("coucou");
            }
        };
        return true;
    }
    public override void AreaChange(AreaInstance area)
    {
    }
    public override void OnUnload()
    {
        MyServer?.Dispose();
        Client?.Dispose();
        base.OnUnload();
    }
    public List<PartyFoe> GetPlayerInfoElementList(List<Entity> entityList)
    {
        var playersInParty = new List<PartyFoe>();
        try
        {
            var partElementList = PartyUI?.Children?[0]?.Children?[0]?.Children;
            if (partElementList != null)
            {
                foreach (var partyElement in partElementList)
                {
                    var playerName = partyElement?.Children?[0]?.Text;

                    var player = entityList.FirstOrDefault(entity =>
                        entity?.GetComponent<Player>()?.PlayerName == playerName)?.GetComponent<Player>();

                    var newElement = new PartyFoe
                    {
                        FoeName = playerName,
                        Foe = player,
                        Element = partyElement,
                        TPButton = partyElement?.Children?[partyElement?.ChildCount == 4 ? 3 : 2]
                    };

                    playersInParty.Add(newElement);
                }
            }
        }
        catch (Exception e)
        {
            LogError($"Character: {e}", 5);
        }
        return playersInParty;
    }

    public override Job Tick()
    {
        isInParty = PartyUI.Children[0].ChildCount > 0 && PartyUI.Children[0].Height > 1;
        if (isInParty && GameController.InGame)
        {
            List<PartyFoe> partyFoes;

            partyFoes = GetPlayerInfoElementList(GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player]);
            Party.Foes = partyFoes;


            foreach (var item in partyFoes)
            {
                if (!Settings.PartyMembers.Values.Contains(item.FoeName))
                {
                    item.I.LogError("error");
                    Settings.PartyMembers.Values.Add(item.FoeName);
                }
            }
        }

        return base.Tick();
    }


    public override void Render()
    {
        try
        {
            Party.Foes.ForEach(e =>
            {
                Graphics.DrawFrame(e.TPButton.GetClientRectCache, Color.Red, 1);
                if (e.Foe != null)
                {

                    var wts = GameController.IngameState.Camera.WorldToScreen(e.Foe.Owner.PosNum);
                    if (GameController.Window.GetWindowRectangle().Contains(wts))
                    {
                        //Graphics.DrawBoundingBoxInWorld(e.PartyPlayer.AsObject<Entity>().PosNum,Color.Red,e.PartyPlayer.AsObject<Entity>().BoundsCenterPosNum,10.0f);
                        this.DrawEllipseToWorld(e.Foe.Owner.PosNum, 20, 25, 2, new Color(80, 0, 1, 80));
                    }
                }
            });
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
        }
    }

    public override void EntityAdded(Entity entity)
    {
    }
}