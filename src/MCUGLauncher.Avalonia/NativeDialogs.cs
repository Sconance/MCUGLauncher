using System.Runtime.InteropServices;
using System.Text;

namespace MCUGLauncher.Avalonia;

/// <summary>Win32 同步对话框，避免把 IDesktopHost 改成异步。</summary>
internal static class NativeDialogs
{
    const uint MB_YESNO = 0x4;
    const uint MB_ICONWARNING = 0x30;
    const int IDYES = 6;
    const int OFN_FILEMUSTEXIST = 0x1000;
    const int OFN_PATHMUSTEXIST = 0x800;
    const int OFN_OVERWRITEPROMPT = 0x2;
    const uint BIF_RETURNONLYFSDIRS = 0x1;

    public static bool Confirm(IntPtr owner, string text, string caption) =>
        MessageBoxW(owner, text, caption, MB_YESNO | MB_ICONWARNING) == IDYES;

    public static string? OpenFile(IntPtr owner, string filter, string? initialDirectory)
    {
        var dialog = Create(owner, filter, initialDirectory);
        return GetOpenFileNameW(ref dialog) ? dialog.lpstrFile.ToString() : null;
    }

    public static string? SaveFile(IntPtr owner, string filter, string defaultName, string? initialDirectory)
    {
        var dialog = Create(owner, filter, initialDirectory);
        dialog.lpstrFile = new StringBuilder(defaultName, 4096);
        dialog.Flags |= OFN_OVERWRITEPROMPT;
        return GetSaveFileNameW(ref dialog) ? dialog.lpstrFile.ToString() : null;
    }

    public static string? PickFolder(IntPtr owner, string title)
    {
        var info = new BrowseInfo
        {
            hwndOwner = owner,
            pszDisplayName = new StringBuilder(260),
            lpszTitle = title,
            ulFlags = BIF_RETURNONLYFSDIRS
        };
        var id = SHBrowseForFolderW(ref info);
        if (id == IntPtr.Zero)
            return null;
        try
        {
            var path = new StringBuilder(260);
            return SHGetPathFromIDListW(id, path) ? path.ToString() : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(id);
        }
    }

    static OpenFileName Create(IntPtr owner, string filter, string? initialDirectory) => new()
    {
        lStructSize = Marshal.SizeOf<OpenFileName>(),
        hwndOwner = owner,
        lpstrFilter = filter,
        lpstrFile = new StringBuilder(4096),
        nMaxFile = 4096,
        lpstrInitialDir = initialDirectory,
        Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST
    };

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(IntPtr hwnd, string text, string caption, uint type);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetOpenFileNameW(ref OpenFileName dialog);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetSaveFileNameW(ref OpenFileName dialog);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SHBrowseForFolderW(ref BrowseInfo info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder path);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string? lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public StringBuilder lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct BrowseInfo
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public StringBuilder pszDisplayName;
        public string? lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }
}
