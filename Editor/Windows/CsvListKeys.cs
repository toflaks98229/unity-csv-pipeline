using UnityEngine;

namespace CsvPipeline
{
    /// <summary>Commands the list can receive from a key press.</summary>
    public enum CsvListCommand
    {
        /// <summary>This key has no meaning here.</summary>
        None,

        /// <summary>Moves to the item above.</summary>
        MoveUp,

        /// <summary>Moves to the item below.</summary>
        MoveDown,

        /// <summary>Moves to the first item.</summary>
        MoveFirst,

        /// <summary>Moves to the last item.</summary>
        MoveLast,

        /// <summary>Expands the selected item.</summary>
        Expand,

        /// <summary>Collapses the selected item.</summary>
        Collapse,

        /// <summary>Flips the selected item between expanded and collapsed.</summary>
        Toggle,

        /// <summary>Bakes the selected item.</summary>
        Activate,

        /// <summary>Moves focus to the search field.</summary>
        Find,

        /// <summary>Clears the search term.</summary>
        ClearSearch,
    }

    /// <summary>
    /// Translates key presses into list commands.
    /// <para>
    /// The Unity Editor Design System requires that <b>every screen be reachable with the keyboard
    /// alone, without a mouse</b> (US-0180). This window's list was not meeting that requirement —
    /// expanding and baking both took a mouse.
    /// </para>
    /// <para>
    /// Which key means what is an <b>invisible judgment</b>, so it is kept apart from the drawing code.
    /// Leave it inside the drawing code and there is no way to check it without a screen.
    /// </para>
    /// </summary>
    public static class CsvListKeys
    {
        /// <summary>
        /// Translates one key into a command. A key with no meaning gives <see cref="CsvListCommand.None"/>.
        /// </summary>
        /// <param name="type">Event type. Only <see cref="EventType.KeyDown"/> is considered.</param>
        /// <param name="key">Key that was pressed.</param>
        /// <param name="modifiers">Modifier keys held at the same time.</param>
        /// <returns>Command to issue.</returns>
        public static CsvListCommand Read(EventType type, KeyCode key, EventModifiers modifiers)
        {
            if (type != EventType.KeyDown) return CsvListCommand.None;

            // 찾기는 보조 키가 있어야 합니다. 윈도우·리눅스는 Ctrl, macOS는 Command 입니다.
            bool command = (modifiers & (EventModifiers.Control | EventModifiers.Command)) != 0;
            if (command) return key == KeyCode.F ? CsvListCommand.Find : CsvListCommand.None;

            // 나머지 명령에는 보조 키가 붙으면 안 됩니다. Alt+↓ 같은 조합은 다른 뜻으로 예약돼 있습니다.
            if ((modifiers & (EventModifiers.Alt | EventModifiers.Shift)) != 0) return CsvListCommand.None;

            switch (key)
            {
                case KeyCode.UpArrow: return CsvListCommand.MoveUp;
                case KeyCode.DownArrow: return CsvListCommand.MoveDown;
                case KeyCode.Home: return CsvListCommand.MoveFirst;
                case KeyCode.End: return CsvListCommand.MoveLast;
                case KeyCode.RightArrow: return CsvListCommand.Expand;
                case KeyCode.LeftArrow: return CsvListCommand.Collapse;
                case KeyCode.Space: return CsvListCommand.Toggle;
                case KeyCode.Return:
                case KeyCode.KeypadEnter: return CsvListCommand.Activate;
                case KeyCode.Escape: return CsvListCommand.ClearSearch;
                default: return CsvListCommand.None;
            }
        }

        /// <summary>
        /// Applies a command to the selected index and returns the new one.
        /// An empty list gives -1. At either end it <b>does not wrap</b> — wrapping hides how far you
        /// have come, so the person never feels the end of the list.
        /// </summary>
        /// <param name="command">Command that was issued.</param>
        /// <param name="current">Currently selected index. -1 when nothing is selected.</param>
        /// <param name="count">Length of the list.</param>
        /// <returns>Index that should now be selected.</returns>
        public static int Move(CsvListCommand command, int current, int count)
        {
            if (count <= 0) return -1;

            switch (command)
            {
                case CsvListCommand.MoveUp: return current <= 0 ? 0 : current - 1;
                case CsvListCommand.MoveDown: return current < 0 ? 0 : Mathf.Min(current + 1, count - 1);
                case CsvListCommand.MoveFirst: return 0;
                case CsvListCommand.MoveLast: return count - 1;
                default: return Mathf.Clamp(current, -1, count - 1);
            }
        }
    }
}
