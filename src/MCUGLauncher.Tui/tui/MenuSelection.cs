namespace MCUGLauncher.Tui;

/// <summary>编号菜单的键盘选择状态；最后一项固定表示返回，不访问终端或业务。</summary>
internal sealed class MenuSelection
{
    readonly int choiceCount;
    public int Selected
    {
        get; private set;
    }

    public MenuSelection(int choiceCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(choiceCount);
        this.choiceCount = choiceCount;
    }

    public int? Handle(ConsoleKey key, int pageSize)
    {
        pageSize = Math.Max(1, pageSize);
        switch (key)
        {
            case ConsoleKey.UpArrow:
                Selected = Selected == 0 ? choiceCount : Selected - 1;
                break;
            case ConsoleKey.DownArrow:
                Selected = Selected == choiceCount ? 0 : Selected + 1;
                break;
            case ConsoleKey.PageUp:
                Selected = Math.Max(0, Selected - pageSize);
                break;
            case ConsoleKey.PageDown:
                Selected = Math.Min(choiceCount, Selected + pageSize);
                break;
            case ConsoleKey.Home:
                Selected = 0;
                break;
            case ConsoleKey.End:
                Selected = choiceCount;
                break;
            case ConsoleKey.Enter:
            case ConsoleKey.RightArrow:
                return Selected == choiceCount ? -1 : Selected;
            case ConsoleKey.LeftArrow:
            case ConsoleKey.Escape:
            case ConsoleKey.D0:
            case ConsoleKey.NumPad0:
                return -1;
            case ConsoleKey.Q:
                throw new EndOfStreamException();
        }
        return null;
    }
}
