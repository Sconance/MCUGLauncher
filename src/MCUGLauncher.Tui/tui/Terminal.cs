using System.Globalization;
using System.Text;

namespace MCUGLauncher.Tui;

/// <summary>终端输入输出、编号选择及确认；不访问配置或业务服务。</summary>
internal sealed class Terminal(TextReader input, TextWriter output)
{
    public void Write(string text = "") => output.WriteLine(new string(text
        .Where(c => !char.IsControl(c) || c is '\n' or '\t').ToArray()));

    public string Read(string prompt)
    {
        Write(prompt);
        var value = input.ReadLine() ?? throw new EndOfStreamException();
        if (value.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
            throw new EndOfStreamException();
        return value.Trim();
    }

    public string Required(string prompt)
    {
        while (true)
        {
            var value = Read(prompt);
            if (value.Length > 0)
                return value;
            Write("请输入内容，或输入 q 退出。");
        }
    }

    public string Password()
    {
        if (Console.IsInputRedirected || !ReferenceEquals(input, Console.In))
        {
            Write("密码（输入内容不会写入 TUI 日志）：");
            return input.ReadLine() ?? throw new EndOfStreamException();
        }
        Write("密码（输入隐藏；Esc 取消）：");
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Escape)
                return "";
            if (key.Key == ConsoleKey.Enter)
                return password.ToString();
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                password.Length--;
            else if (!char.IsControl(key.KeyChar))
                password.Append(key.KeyChar);
        }
    }

    public bool Confirm(string prompt) => Read(prompt + " [y/N]").Equals("y", StringComparison.OrdinalIgnoreCase);

    public int Select(string title, IReadOnlyList<string> choices)
    {
        Write();
        Write("=== " + title + " ===");
        for (var i = 0; i < choices.Count; i++)
            Write($"{i + 1}. {choices[i]}");
        Write("0. 返回 / 退出    q. 退出程序");
        while (true)
        {
            var value = Read("请输入编号：");
            if (int.TryParse(value, out var number) && number >= 0 && number <= choices.Count)
                return number - 1;
            Write("编号无效，请重新输入。");
        }
    }

    public int Number(string prompt, int min, int max)
    {
        while (true)
        {
            if (int.TryParse(Read(prompt), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                && number >= min && number <= max)
                return number;
            Write($"请输入 {min} 到 {max} 之间的整数。");
        }
    }

    public void Pause() => Read("按回车继续（q 退出）：");
}
