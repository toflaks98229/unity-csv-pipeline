using UnityEngine;

namespace CsvPipeline
{
    /// <summary>목록에서 키로 내릴 수 있는 명령입니다.</summary>
    public enum CsvListCommand
    {
        /// <summary>이 키에는 뜻이 없습니다.</summary>
        None,

        /// <summary>위 항목으로 갑니다.</summary>
        MoveUp,

        /// <summary>아래 항목으로 갑니다.</summary>
        MoveDown,

        /// <summary>첫 항목으로 갑니다.</summary>
        MoveFirst,

        /// <summary>마지막 항목으로 갑니다.</summary>
        MoveLast,

        /// <summary>고른 항목을 펼칩니다.</summary>
        Expand,

        /// <summary>고른 항목을 접습니다.</summary>
        Collapse,

        /// <summary>고른 항목의 펼침을 뒤집습니다.</summary>
        Toggle,

        /// <summary>고른 항목을 굽습니다.</summary>
        Activate,

        /// <summary>검색 칸으로 초점을 옮깁니다.</summary>
        Find,

        /// <summary>검색어를 지웁니다.</summary>
        ClearSearch,
    }

    /// <summary>
    /// 키 입력을 목록 명령으로 옮깁니다.
    /// <para>
    /// Unity Editor Design System 은 <b>모든 화면이 마우스 없이 키보드만으로 닿을 수 있어야 한다</b>고
    /// 요구합니다(US-0180). 이 창의 목록은 그 요구를 지키지 못하고 있었습니다 — 펼치기도 굽기도
    /// 마우스로만 되었습니다.
    /// </para>
    /// <para>
    /// 어느 키가 무슨 뜻인지는 <b>눈에 보이지 않는 판단</b>이라 그리기에서 떼어 둡니다.
    /// 그리기 안에 두면 화면 없이 확인할 방법이 없어집니다.
    /// </para>
    /// </summary>
    public static class CsvListKeys
    {
        /// <summary>
        /// 키 하나를 명령으로 옮깁니다. 뜻이 없으면 <see cref="CsvListCommand.None"/> 입니다.
        /// </summary>
        /// <param name="type">이벤트 종류입니다. <see cref="EventType.KeyDown"/> 만 봅니다.</param>
        /// <param name="key">눌린 키입니다.</param>
        /// <param name="modifiers">함께 눌린 보조 키입니다.</param>
        /// <returns>내릴 명령입니다.</returns>
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
        /// 명령을 골라 놓은 자리에 적용해 새 자리를 돌려줍니다.
        /// 목록이 비어 있으면 -1 입니다. 끝에서는 <b>감싸지 않습니다</b> — 감싸면 어디까지 왔는지
        /// 알 수 없어 사람이 목록의 끝을 느끼지 못합니다.
        /// </summary>
        /// <param name="command">내려진 명령입니다.</param>
        /// <param name="current">지금 고른 자리입니다. 고른 것이 없으면 -1 입니다.</param>
        /// <param name="count">목록의 길이입니다.</param>
        /// <returns>새로 골라야 할 자리입니다.</returns>
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
