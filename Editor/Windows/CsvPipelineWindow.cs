using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// A window that handles tables, sheet sync, and settings on one screen.
    /// Scattered across menu items, there is no single place to see what state everything is in.
    /// <b>This window acts only when someone presses a button.</b> Leaving it open changes nothing.
    /// </summary>
    public sealed partial class CsvPipelineWindow : EditorWindow
    {
        /// <summary>The tabs the screen is split into.</summary>
        private enum Tab
        {
            /// <summary>Tables and their output assets.</summary>
            Tables,

            /// <summary>Google Sheet sync.</summary>
            Sheets,

            /// <summary>Settings such as folder locations.</summary>
            Settings
        }

        private static readonly string[] TabLabels = { "Tables", "Sheet Sync", "Settings" };

        /// <summary>
        /// Prefix for the keys that hold the window state.
        /// Every script edit triggers a domain reload, and plain fields would send the tab, the search
        /// text, and every expanded row back to their defaults. Storing them in <see cref="SessionState"/>,
        /// which Unity's own tools use, carries them across.
        /// </summary>
        private const string StateKey = "CsvPipeline.Window.";

        /// <summary>The tab currently on screen.</summary>
        private Tab CurrentTab
        {
            get => (Tab)SessionState.GetInt(StateKey + "Tab", 0);
            set => SessionState.SetInt(StateKey + "Tab", (int)value);
        }

        /// <summary>
        /// Opens the window. <b>The window title matches this menu item</b> — once it is open, you should
        /// still be able to tell which command opened it. (Unity Editor Design System)
        /// </summary>
        [MenuItem("Tools/CSV Pipeline/Pipeline Window", false, 0)]
        public static void Open()
        {
            var window = GetWindow<CsvPipelineWindow>();
            window.titleContent = CsvEditorUI.IconAnd("d_ScriptableObject Icon", "CSV Pipeline");
            window.minSize = new Vector2(560, 360);
            window.Invalidate();
        }

        /// <summary>Marks the window for a rescan when it opens.</summary>
        private void OnEnable() => Invalidate();

        /// <summary>
        /// Takes mouse-move events while the window is in front. Highlighting the row under the cursor
        /// means repainting on every move, and <b>there is no reason to pay that for a window nobody is
        /// looking at.</b>
        /// </summary>
        private void OnFocus() => wantsMouseMove = true;

        /// <summary>Stops taking mouse-move events when the window loses focus, and clears the leftover highlight.</summary>
        private void OnLostFocus()
        {
            wantsMouseMove = false;
            _hoverIndex = -1;
            Repaint();
        }

        /// <summary>Drops the cached lists so the next draw collects them again.</summary>
        private void Invalidate()
        {
            _scanned = false;
            _sheets = null;
            CsvPipelineSettings.InvalidateCache();
        }

        /// <summary>Draws the window.</summary>
        private void OnGUI()
        {
            DrawTabs();

            switch (CurrentTab)
            {
                case Tab.Tables: DrawTables(); break;
                case Tab.Sheets: DrawSheets(); break;
                default: DrawSettings(); break;
            }

            DrawStatusBar();
        }

        /// <summary>The row that selects a tab.</summary>
        private void DrawTabs()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                Tab current = CurrentTab;

                for (int i = 0; i < TabLabels.Length; i++)
                {
                    bool on = (int)current == i;
                    if (GUILayout.Toggle(on, TabLabels[i], EditorStyles.toolbarButton, GUILayout.Width(84)) && !on)
                    {
                        CurrentTab = (Tab)i;
                        GUI.FocusControl(null);
                    }
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(CsvEditorUI.IconOr("_Help", "?", Shortcuts),
                                     EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    CsvDocs.Open();
                }
            }
        }

        /// <summary>
        /// The tooltip on the help button. Nothing on screen shows what the keys do, so unless it is
        /// written down people never find out the keys exist.
        /// </summary>
        private const string Shortcuts =
            "Opens the documentation.\n\n"
            + "Keys in the Tables tab\n"
            + "  ↑ ↓ · Home · End    Select a table\n"
            + "  → ←                 Expand · Collapse\n"
            + "  Space               Toggle expansion\n"
            + "  Enter               Bake the selected table\n"
            + "  Ctrl(⌘)+F           Find\n"
            + "  Esc                 Clear the search\n"
            + "  Right-click         Menu for that table";

        /// <summary>
        /// The single line at the bottom of the window. It reports the current state and what this window
        /// is, always in the same place. Put it in the toolbar and it blends into the buttons; spread it
        /// across the tabs and it moves every time you look for it.
        /// </summary>
        private void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(StatusText(), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                CsvEditorUI.ColoredLabel("Nothing is written until you press a button", CsvEditorUI.Muted);
            }
        }

        /// <summary>The status line for the current tab.</summary>
        /// <returns>Status text.</returns>
        private string StatusText()
        {
            switch (CurrentTab)
            {
                case Tab.Tables: return TablesStatus();
                case Tab.Sheets: return SheetsStatus();
                default: return CsvPipelineSettings.ExistsInProject ? "Settings asset in use" : "No settings asset — defaults";
            }
        }
    }
}
