using System.Runtime.InteropServices;

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

    // IFileDialog 选项：让「打开文件」这套对话框改成选目录
    const uint FOS_PICKFOLDERS = 0x20;
    const uint FOS_FORCEFILESYSTEM = 0x40;

    const uint FOS_PATHMUSTEXIST = 0x8;

    // IShellItem.GetDisplayName 的形式：返回文件系统路径
    const uint SIGDN_FILESYSPATH = 0x80058000;

    const int PathBufferChars = 4096;

    public static bool Confirm(IntPtr owner, string text, string caption) =>
        MessageBoxW(owner, text, caption, MB_YESNO | MB_ICONWARNING) == IDYES;

    public static string? OpenFile(IntPtr owner, string filter, string? initialDirectory)
    {
        var dialog = Create(owner, filter, initialDirectory);
        try
        {
            return GetOpenFileNameW(ref dialog) ? ReadWideString(dialog.lpstrFile) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(dialog.lpstrFile);
        }
    }

    public static string? SaveFile(IntPtr owner, string filter, string defaultName, string? initialDirectory)
    {
        var dialog = Create(owner, filter, initialDirectory);
        try
        {
            WriteWideString(dialog.lpstrFile, PathBufferChars, defaultName);
            dialog.Flags |= OFN_OVERWRITEPROMPT;
            return GetSaveFileNameW(ref dialog) ? ReadWideString(dialog.lpstrFile) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(dialog.lpstrFile);
        }
    }

    public static string? PickFolder(IntPtr owner, string title)
    {
        // 用 IFileOpenDialog + FOS_PICKFOLDERS：与「打开文件」是同一个现代对话框，
        // 只是把「选择」按钮的语义改成选目录（老 SHBrowseForFolder 是另一套树形窗口）。
        var dialog = (IFileDialog)new FileOpenDialog();
        try
        {
            dialog.GetOptions(out var options);
            dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);
            dialog.SetTitle(title);
            // 用户取消返回 HRESULT_FROM_WIN32(ERROR_CANCELLED)，这里统一当作“没选”
            if (dialog.Show(owner) != 0)
                return null;

            dialog.GetResult(out var item);
            try
            {
                item.GetDisplayName(SIGDN_FILESYSPATH, out var path);
                return string.IsNullOrEmpty(path) ? null : path;
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    static OpenFileName Create(IntPtr owner, string filter, string? initialDirectory) => new()
    {
        lStructSize = Marshal.SizeOf<OpenFileName>(),
        hwndOwner = owner,
        lpstrFilter = filter,
        lpstrFile = AllocZeroed(PathBufferChars),
        nMaxFile = PathBufferChars,
        lpstrInitialDir = initialDirectory,
        Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST
    };

    static IntPtr AllocZeroed(int chars)
    {
        var buffer = Marshal.AllocHGlobal(chars * sizeof(char));
        for (var i = 0; i < chars; i++)
            Marshal.WriteInt16(buffer, i * sizeof(char), 0);
        return buffer;
    }

    static string ReadWideString(IntPtr buffer) => Marshal.PtrToStringUni(buffer) ?? "";

    static void WriteWideString(IntPtr buffer, int capacity, string value)
    {
        var count = Math.Min(value.Length, capacity - 1);
        for (var i = 0; i < count; i++)
            Marshal.WriteInt16(buffer, i * sizeof(char), value[i]);
        Marshal.WriteInt16(buffer, count * sizeof(char), 0);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(IntPtr hwnd, string text, string caption, uint type);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetOpenFileNameW(ref OpenFileName dialog);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetSaveFileNameW(ref OpenFileName dialog);

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
        public IntPtr lpstrFile;
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

    // FileOpenDialog 组件：CoCreateInstance 由 CLR 在 new 时完成
    [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    class FileOpenDialog
    {
    }

    [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileDialog
    {
        // IModalWindow
        [PreserveSig]
        int Show(IntPtr parent);

        // 以下顺序必须与原生 vtable 一致；只声明到 GetResult 为止，后面没用到的不声明
        void SetFileTypes(uint count, IntPtr filterSpec);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem folder);
        void SetFolder(IShellItem folder);
        void GetFolder(out IShellItem folder);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr bindCtx, ref Guid handler, ref Guid id, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint form, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }
}
