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
using GameOffsets;
using GameOffsets.Native;
using Party_Plugin.PathFinding;
using C5;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace Party_Plugin
{
    public interface IPartyPluginInstance
    {
        public PartyPlugin I { get; }
    }

    public struct MyTerrain
    {
        public TerrainData TerrainMetaData;
        public float[][] HeighData;
        public Vector2i AreaDimensions;
        public int[][] ProcessedTerrainData;
    }

    public class PartyPlugin : BaseSettingsPlugin<PartyPluginSettings>, IPartyPluginInstance
    {
        private bool isInParty = false;
        private Party.Party Party = new();
        private MyServer MyServer = new();
        private MyClient Client = new();
        private const int TileToGridConversion = 23;
        private const int TileToWorldConversion = 250;
        public const float GridToWorldMultiplier = TileToWorldConversion / (float)TileToGridConversion;
        private const double CameraAngle = 38.7 * Math.PI / 180;
        private static readonly float CameraAngleCos = (float)Math.Cos(CameraAngle);
        private static readonly float CameraAngleSin = (float)Math.Sin(CameraAngle);

        public PathFinder PathFinder;

        public Camera Cam => GameController.IngameState.Camera;
        public Element PartyUI => GameController.IngameState.IngameUi.PartyElement;
        public Player P => GameController.Player.GetComponent<Player>();

        public MyTerrain MyTerrain;
        private List<List<Vector2i>> test = new();

        public override bool Initialise()
        {
            Settings.PartySettings.PartyMemberType.Values.AddRange(new string[]{
        "Follower","Leader"
        });

            Settings.Connect.OnPressed += async delegate
            {
                if (Settings.PartySettings.PartyMemberType.Value == "Leader")
                {
                    if (MyServer == null)
                    {
                        await MyServer.StartServer();
                    }
                    else
                    {
                        if (!MyServer.IsServerRunning)
                            await MyServer.StartServer();
                    }
                }
                else
                {
                    if (Client == null)
                    {
                        Client = new MyClient();
                        await Client.StartClient(GameController.Player.GetComponent<Player>());
                    }
                    else
                    {
                        if (!Client.IsClientRunning)
                            await Client.StartClient(GameController.Player.GetComponent<Player>());
                    }
                }
            };

            Settings.Foo.OnPressed += async delegate
            {
                if (Settings.PartySettings.PartyMemberType.Value == "Leader")
                {
                    if (MyServer != null)
                    {
                        MyServer.BroadcastMessage(new Message(MessageType.None, "coucou", Client.ClientInstance));
                    }
                }
                else
                {
                    if (Client == null)
                    {
                        Client = new MyClient();
                        await Client.StartClient(P);
                    }
                    if (!Client.IsClientRunning)
                        await Client.StartClient(P);

                    if (Client != null && Client.ClientInstance != null)
                        await Client.SendMessageToServer(new Message(MessageType.None, "coucou", Client.ClientInstance));
                    else
                        LogMsg("Client or ClientInstance is null");
                }
            };

            MyTerrain.TerrainMetaData = GameController.IngameState.Data.DataStruct.Terrain;
            MyTerrain.HeighData = GameController.IngameState.Data.RawTerrainHeightData;
            MyTerrain.AreaDimensions = GameController.IngameState.Data.AreaDimensions;
            MyTerrain.ProcessedTerrainData = GameController.IngameState.Data.RawPathfindingData;
            PathFinder = new PathFinder(MyTerrain.ProcessedTerrainData, new[] { 1, 2, 3, 4, 5 });
            if (Party.Foes == null || Party.Foes.Count == 0)
                Party.Foes = GetPlayerInfoElementList(GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player]);
            firstScan = null;
            return true;
        }

        public override void AreaChange(AreaInstance area)
        {
            MyTerrain.TerrainMetaData = GameController.IngameState.Data.DataStruct.Terrain;
            MyTerrain.HeighData = GameController.IngameState.Data.RawTerrainHeightData;
            MyTerrain.AreaDimensions = GameController.IngameState.Data.AreaDimensions;
            MyTerrain.ProcessedTerrainData = GameController.IngameState.Data.RawPathfindingData;

            if (Party.Foes == null || Party.Foes.Count == 0)
                Party.Foes = GetPlayerInfoElementList(GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player]);
            firstScan = null;
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
                        var playerNameElement = partyElement?.Children?[0];
                        var playerName = playerNameElement?.Text;

                        if (!string.IsNullOrEmpty(playerName))
                        {
                            var player = entityList.FirstOrDefault(entity =>
                                entity?.GetComponent<Player>()?.PlayerName == playerName)?.GetComponent<Player>();

                            var tpButtonIndex = (partyElement?.ChildCount == 4) ? 3 : 2;
                            var tpButton = (tpButtonIndex >= 0 && tpButtonIndex < partyElement.ChildCount)
                                ? partyElement.Children[tpButtonIndex]
                                : null;

                            var newElement = new PartyFoe
                            {
                                FoeName = playerName,
                                Foe = player,
                                Element = partyElement,
                                TPButton = tpButton
                            };

                            playersInParty.Add(newElement);
                        }
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
                if (Party.Foes == null || Party.Foes.Count == 0 || Party.Foes.Any(f => f.Foe == null))
                {
                    Party.Foes = GetPlayerInfoElementList(GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player]);
                    LogMessage("Updating foes from tick", 5);
                }

                LogMessage(Party.Foes.Count.ToString() + " tick party foes");

                test.Clear();

                foreach (var item in Party.Foes)
                {
                    if (item.Foe != null)
                    {
                        LogMessage($"Follower: {item.FoeName}, Pos: {item.Foe.Owner.GridPosNum}");

                        var path = PathFinder.FindPath(P.Owner.GridPosNum.RoundToVector2I(), item.Foe.Owner.GridPosNum.RoundToVector2I());
                        if (path != null && path.Count > 0)
                        {
                            test.Add(path);
                        }
                        else
                        {
                            LogMessage($"No valid path found for {item.FoeName}");
                        }
                    }
                    else
                    {
                        LogMessage($"Foe is null for {item.FoeName}");
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
                if (e.TPButton != null)
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
                }
                else
                {
                    LogMessage($"TPButton is null for {e.FoeName}");
                }
            });

            FooUiTest();

            Graphics.DrawFrame(GameController.IngameState.IngameUi.SkillBar.GetClientRectCache, Color.Red, 2);

            if (test != null && test.Count > 0)
            {
                foreach (var item in test)
                {
                    if (item != null)
                    {
                        foreach (var item2 in item)
                        {
                            if (item2 != null)
                            {
                                var wts = GameController.IngameState.Camera.WorldToScreen(
                                    new Vector3(item2.X * GridToWorldMultiplier, item2.Y * GridToWorldMultiplier, GetTerrainHeight(item2)));

                                if (GameController.Window.GetWindowRectangle().Contains(wts))
                                {
                                    Graphics.DrawBox(wts, wts + 10, Color.Red);
                                }
                            }
                            else
                            {
                                LogMessage($"item2 is null");
                            }
                        }
                    }
                    else
                    {
                        LogMessage($"item is null");
                    }
                }
            }
        }

        private float GetTerrainHeight(Vector2i coordinates)
        {
            if (coordinates != null && coordinates.X >= 0 && coordinates.Y >= 0 && coordinates.Y < MyTerrain.HeighData.Length && coordinates.X < MyTerrain.HeighData[coordinates.Y].Length)
            {
                return MyTerrain.HeighData[coordinates.Y][coordinates.X];
            }

            LogMessage($"Invalid coordinates: {coordinates}");
            return 0; // or a default value
        }
        public void FooUiTest()
        {
            if (Input.IsKeyDown(Keys.LShiftKey) && Settings.PartySettings.PartyMemberType.Value == "Leader")
            {
                var test = GameController.IngameState.IngameUi.SkillBar.GetClientRectCache;
                ImGui.SetNextWindowPos(new System.Numerics.Vector2(test.TopLeft.X, test.TopLeft.Y - 20));
                ImGui.Begin("", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar);
                ImGui.SetCursorPos(new System.Numerics.Vector2(ImGui.GetCursorPosX(), ImGui.GetCursorPosY() - 10));
                if (ImGui.Button("H"))
                {
                }
                ImGui.SameLine();
                ImGui.SetCursorPos(new System.Numerics.Vector2(ImGui.GetCursorPosX() + 10, ImGui.GetCursorPosY()));
                if (ImGui.Button("P"))
                {
                }
                ImGui.SameLine();
                ImGui.SetCursorPos(new System.Numerics.Vector2(ImGui.GetCursorPosX() + 10, ImGui.GetCursorPosY()));
                if (ImGui.Button("M"))
                {
                }
                ImGui.End();
            }
        }

        public override void EntityAdded(Entity entity)
        {
        }

        // IPartyPluginInstance implementation
        public PartyPlugin I => this;

        public IEnumerable<List<Vector2i>> firstScan { get; private set; }
    }

}
