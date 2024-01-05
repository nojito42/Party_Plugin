using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using Party_Plugin.Helpers;
using Party_Plugin.Party;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using Party_Plugin.Myserver;
using ImGuiNET;
using System.Windows.Forms;
using Message = Party_Plugin.Myserver.Message;
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
        Settings.PartySettings.PartyMemberType.Values.AddRange(new string[]{
            "Follower","Leader"
        });

        Settings.Connect.OnPressed += delegate
        {
            if (Settings.PartySettings.PartyMemberType.Value == "Leader")
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
            if (Settings.PartySettings.PartyMemberType.Value == "Leader")
            {
                MyServer.BroadcastMessage(new Message(MessageType.none, "coucou"));
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
                    await Client.SendMessageToServer(new Message(MessageType.none, "coucou"));
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
        isInParty = PartyUI != null && PartyUI.Height > 1;
        if (isInParty && GameController.InGame)
        {
            LogMessage("PartyPlugin: In Party");
            List<PartyFoe> partyFoes;

            partyFoes = GetPlayerInfoElementList(GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player]);
            Party.Foes = partyFoes;


            foreach (var item in partyFoes)
            {
                if (!Settings.PartySettings.PartyMembers.Values.Contains(item.FoeName))
                {
                    Settings.PartySettings.PartyMembers.Values.Add(item.FoeName);
                }
            }
        }
        else if (!isInParty)
        {
            LogMessage("PartyPlugin: Not In Party");
            Party.Foes.Clear();
        }

        return base.Tick();
    }
    public override void Render()
    {

        Party.Foes?.ForEach(e =>
        {
            Graphics.DrawFrame(e.TPButton.GetClientRectCache, Color.Red, 1);
            if (e.Foe != null)
            {

                var wts = GameController.IngameState.Camera.WorldToScreen(e.Foe.Owner.PosNum);
                if (GameController.Window.GetWindowRectangle().Contains(wts))
                {
                    this.DrawEllipseToWorld(e.Foe.Owner.PosNum, 20, 25, 2, new Color(80, 0, 1, 80));
                }
            }
        });
        FooUiTest();

        Graphics.DrawFrame(GameController.IngameState.IngameUi.SkillBar.GetClientRectCache, Color.Red, 2);

     
    }

    public void FooUiTest()
    {
        if (Input.IsKeyDown(Keys.LShiftKey) && Settings.PartySettings.PartyMemberType.Value == "Leader")
        {
            var test = GameController.IngameState.IngameUi.SkillBar.GetClientRectCache;

            // Set the window position
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(test.TopLeft.X, test.TopLeft.Y -20));

            // Begin a window with no title bar, no resize, and no scrollbar
            ImGui.Begin("", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar);

            // Set the cursor position relative to the existing ImGui frame
            ImGui.SetCursorPos(new System.Numerics.Vector2(ImGui.GetCursorPosX(), ImGui.GetCursorPosY() - 10));

            // Draw the first button
            if (ImGui.Button("H"))
            {
                // Handle button click
            }

            // Move to the next button position
            ImGui.SameLine();
            ImGui.SetCursorPos(new System.Numerics.Vector2(ImGui.GetCursorPosX() + 10, ImGui.GetCursorPosY()));

            // Draw the second button
            if (ImGui.Button("P"))
            {
                // Handle button click
            }

            // Move to the next button position
            ImGui.SameLine();
            ImGui.SetCursorPos(new System.Numerics.Vector2(ImGui.GetCursorPosX() + 10, ImGui.GetCursorPosY()));

            // Draw the third button
            if (ImGui.Button("M"))
            {
                // Handle button click
            }

            // End the window
            ImGui.End();
        }


    }
    public override void EntityAdded(Entity entity)
    {
    }
}