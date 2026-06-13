using System;
using System.IO;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public static class SceneDropdownWidget
    {
        public struct DropdownInteraction
        {
            public string SelectedSceneName; // string.Empty if none selected
        }

        public static void LoadAvailableScenes(RendererState state)
        {
            state.DropdownItems.Clear();
            state.DropdownItems.Add(new DropdownItem { Name = "--- WORLD ---", IsHeader = true });
            state.DropdownItems.Add(new DropdownItem { Name = "world", SceneName = "world" });
            
            state.DropdownItems.Add(new DropdownItem { Name = "--- OTHERS ---", IsHeader = true });
            
            // Try AppDomain.CurrentDomain.BaseDirectory + "Content/scenes/encounters"
            var scenesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "scenes", "encounters");
            
            // Fallback to development layout
            if (!Directory.Exists(scenesDir))
            {
                scenesDir = Path.GetFullPath(Path.Combine("..", "Content", "scenes", "encounters"));
            }

            if (Directory.Exists(scenesDir))
            {
                var files = Directory.GetFiles(scenesDir, "*.scm");
                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    state.DropdownItems.Add(new DropdownItem { Name = name, SceneName = name });
                }
            }
        }

        public static DropdownInteraction Draw(RendererState state, SceneManager sceneManager, SSNoir.TerminalApp.Rendering.UiInteractionContext ui, float windowWidth)
        {
            var interaction = new DropdownInteraction { SelectedSceneName = string.Empty };

            var boxRect = new Rectangle(windowWidth - 190, 30, 150, 32);
            bool hoverBox = ui.CanHover(boxRect);
            bool leftClick = !ui.IsLocked && Raylib.IsMouseButtonPressed(MouseButton.Left);

            if (leftClick)
            {
                if (hoverBox)
                {
                    state.IsDropdownOpen = !state.IsDropdownOpen;
                }
                else if (state.IsDropdownOpen)
                {
                    for (int i = 0; i < state.DropdownItems.Count; i++)
                    {
                        var item = state.DropdownItems[i];
                        if (item.IsHeader) continue;

                        var optRect = new Rectangle(boxRect.X, boxRect.Y + boxRect.Height + i * 32, boxRect.Width, 32);
                        if (Raylib.CheckCollisionPointRec(ui.Mouse, optRect))
                        {
                            interaction.SelectedSceneName = item.SceneName;
                            break;
                        }
                    }
                    state.IsDropdownOpen = false;
                }
            }

            // Draw Dropdown Box
            Color boxBgColor = hoverBox ? new Color((byte)50, (byte)50, (byte)70, (byte)255) : new Color((byte)30, (byte)30, (byte)40, (byte)255);
            Color boxOutlineColor = state.IsDropdownOpen ? new Color(130, 130, 220, 255) : new Color(70, 70, 90, 255);
            
            Raylib.DrawRectangleRounded(boxRect, 0.2f, 4, boxBgColor);
            Raylib.DrawRectangleRoundedLinesEx(boxRect, 0.2f, 4, 1.5f, boxOutlineColor);

            string currentScene = sceneManager.CurrentSceneName;
            FontManager.DrawText(currentScene, boxRect.X + 12, boxRect.Y + 6, 15, Color.White);
            FontManager.DrawText("v", boxRect.X + boxRect.Width - 22, boxRect.Y + 6, 14, new Color(150, 150, 170, 255));

            // Draw Options List
            if (state.IsDropdownOpen)
            {
                for (int i = 0; i < state.DropdownItems.Count; i++)
                {
                    var optRect = new Rectangle(boxRect.X, boxRect.Y + boxRect.Height + i * 32, boxRect.Width, 32);
                    var item = state.DropdownItems[i];

                    if (item.IsHeader)
                    {
                        Raylib.DrawRectangleRec(optRect, new Color(20, 20, 25, 255));
                        int lblW = FontManager.MeasureTextWidth(item.Name, 12);
                        FontManager.DrawText(item.Name, optRect.X + (optRect.Width - lblW) / 2f, optRect.Y + 10, 12, new Color(100, 100, 120, 255));
                    }
                    else
                    {
                        bool hoverOpt = ui.CanHover(optRect);
                        Color optBgColor = hoverOpt ? new Color(70, 70, 95, 255) : new Color(25, 25, 35, 255);
                        Color optTextColor = hoverOpt ? Color.White : new Color(180, 180, 200, 255);

                        Raylib.DrawRectangleRec(optRect, optBgColor);
                        if (item.SceneName == currentScene)
                        {
                            Raylib.DrawRectangle((int)optRect.X, (int)optRect.Y, 4, (int)optRect.Height, new Color(100, 100, 250, 255));
                        }
                        
                        FontManager.DrawText(item.Name, optRect.X + 12, optRect.Y + 6, 15, optTextColor);
                    }

                    if (i < state.DropdownItems.Count - 1)
                    {
                        Raylib.DrawLineEx(new System.Numerics.Vector2(optRect.X, optRect.Y + optRect.Height), 
                                         new System.Numerics.Vector2(optRect.X + optRect.Width, optRect.Y + optRect.Height), 
                                         1f, new Color(45, 45, 55, 255));
                    }
                }

                var listRect = new Rectangle(boxRect.X, boxRect.Y + boxRect.Height, boxRect.Width, state.DropdownItems.Count * 32);
                Raylib.DrawRectangleLinesEx(listRect, 1.5f, boxOutlineColor);
            }

            return interaction;
        }
    }
}
