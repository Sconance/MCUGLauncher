using System.Globalization;
using System.Text;

namespace MCUGLauncher.Tui;

/// <summary>交互终端的分页菜单渲染及按键读取；不调用业务接口。</summary>
internal static class ArrowMenu
{
    public static int Select(string title, IReadOnlyList<string> choices, IReadOnlyList<string> context)
    {
        var selection = new MenuSelection(choices.Count);
        var foreground = Console.ForegroundColor;
        var background = Console.BackgroundColor;
        try
        {
            Console.CursorVisible = false;
            while (true)
            {
                // 在每次按键后重取尺寸，缩放窗口后仍可访问列表末尾。
                if (Console.WindowWidth < 24 || Console.WindowHeight < 10)
                    throw new IOException("终端尺寸不足。");
                var width = Console.WindowWidth - 1;
                var height = Console.WindowHeight;
                var contextLines = context.TakeLast(Math.Min(4, Math.Max(0, height - 12))).ToArray();
                var pageSize = Math.Max(1, height - contextLines.Length - 8);
                var first = Math.Min(selection.Selected, Math.Max(0, choices.Count - 1)) / pageSize * pageSize;
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Line("=== " + title + " ===", width);
                Console.ForegroundColor = foreground;
                foreach (var line in contextLines)
                    Line(line, width);
                Line("↑↓ 移动  →/Enter 确认  ←/Esc 返回", width);
                Line("PgUp/PgDn 翻页  Home/End 首尾  Q 退出", width);
                Console.WriteLine();
                for (var i = first; i < Math.Min(first + pageSize, choices.Count); i++)
                    Item($"{i + 1}. {choices[i]}", i == selection.Selected, width, foreground, background);
                if (choices.Count == 0)
                    Line("没有可选项目。", width);
                Item("0. 返回 / 退出", selection.Selected == choices.Count, width, foreground, background);
                var pages = Math.Max(1, (choices.Count + pageSize - 1) / pageSize);
                Line($"第 {first / pageSize + 1}/{pages} 页 | 共 {choices.Count} 项", width);

                var result = selection.Handle(Console.ReadKey(true).Key, pageSize);
                if (result.HasValue)
                    return result.Value;
            }
        }
        finally
        {
            Console.ForegroundColor = foreground;
            Console.BackgroundColor = background;
            Console.CursorVisible = true;
        }
    }

    static void Item(string text, bool selected, int width, ConsoleColor foreground, ConsoleColor background)
    {
        if (selected)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.DarkBlue;
        }
        Line((selected ? "> " : "  ") + text, width);
        Console.ForegroundColor = foreground;
        Console.BackgroundColor = background;
    }

    static void Line(string text, int width) => Console.WriteLine(Fit(text, width));

    // 按显示列截断中英文混排，避免长路径换行覆盖菜单或滚出屏幕。
    internal static string Fit(string text, int width)
    {
        if (width <= 0)
            return "";
        var result = new StringBuilder();
        var columns = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format)
                continue;
            var size = category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark ? 0
                : IsWide(rune.Value) ? 2 : 1;
            if (columns + size > Math.Max(0, width - 1))
            {
                result.Append('…');
                break;
            }
            result.Append(rune.ToString());
            columns += size;
        }
        return result.ToString();
    }

    static bool IsWide(int value) => value is >= 0x1100 and <= 0x115F
        or >= 0x2329 and <= 0x232A or >= 0x2E80 and <= 0xA4CF
        or >= 0xAC00 and <= 0xD7A3 or >= 0xF900 and <= 0xFAFF
        or >= 0xFE10 and <= 0xFE19 or >= 0xFE30 and <= 0xFE6F
        or >= 0xFF00 and <= 0xFF60 or >= 0xFFE0 and <= 0xFFE6
        or >= 0x1F000 and <= 0x1FAFF or >= 0x20000 and <= 0x3FFFD;
}
