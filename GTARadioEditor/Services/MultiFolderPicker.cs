using System.Runtime.InteropServices;

namespace GTARadioEditor.Services;

/// <summary>Shows the native Windows folder picker with Ctrl/Shift multi-selection enabled.</summary>
public static class MultiFolderPicker
{
    private const int UserCancelled = unchecked((int)0x800704C7);

    public static IReadOnlyList<string> Pick(IWin32Window owner, string title)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var dialog = (IFileOpenDialog)new FileOpenDialog();
        IShellItemArray? results = null;
        try
        {
            dialog.GetOptions(out var options);
            dialog.SetOptions(options |
                              FileOpenDialogOptions.PickFolders |
                              FileOpenDialogOptions.AllowMultiSelect |
                              FileOpenDialogOptions.ForceFileSystem |
                              FileOpenDialogOptions.PathMustExist);
            dialog.SetTitle(title);

            var result = dialog.Show(owner.Handle);
            if (result == UserCancelled)
            {
                return [];
            }
            Marshal.ThrowExceptionForHR(result);

            dialog.GetResults(out results);
            results.GetCount(out var count);
            var folders = new List<string>((int)count);
            for (uint index = 0; index < count; index++)
            {
                results.GetItemAt(index, out var item);
                try
                {
                    item.GetDisplayName(ShellDisplayName.FileSystemPath, out var pathPointer);
                    try
                    {
                        var path = Marshal.PtrToStringUni(pathPointer);
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            folders.Add(path);
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pathPointer);
                    }
                }
                finally
                {
                    Marshal.FinalReleaseComObject(item);
                }
            }

            return folders
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            if (results is not null)
            {
                Marshal.FinalReleaseComObject(results);
            }
            Marshal.FinalReleaseComObject(dialog);
        }
    }

    [Flags]
    private enum FileOpenDialogOptions : uint
    {
        PickFolders = 0x00000020,
        ForceFileSystem = 0x00000040,
        AllowMultiSelect = 0x00000200,
        PathMustExist = 0x00000800
    }

    private enum ShellDisplayName : uint
    {
        FileSystemPath = 0x80058000
    }

    [ComImport]
    [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    [ClassInterface(ClassInterfaceType.None)]
    private class FileOpenDialog;

    [ComImport]
    [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig]
        int Show(IntPtr parent);

        void SetFileTypes(uint fileTypeCount, IntPtr fileTypes);
        void SetFileTypeIndex(uint fileTypeIndex);
        void GetFileTypeIndex(out uint fileTypeIndex);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(FileOpenDialogOptions options);
        void GetOptions(out FileOpenDialogOptions options);
        void SetDefaultFolder(IShellItem folder);
        void SetFolder(IShellItem folder);
        void GetFolder(out IShellItem folder);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, int placement);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int result);
        void SetClientGuid(ref Guid clientGuid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
        void GetResults(out IShellItemArray results);
        void GetSelectedItems(out IShellItemArray items);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(ShellDisplayName displayName, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport]
    [Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        void BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);
        void GetPropertyStore(int flags, ref Guid interfaceId, out IntPtr result);
        void GetPropertyDescriptionList(IntPtr keyType, ref Guid interfaceId, out IntPtr result);
        void GetAttributes(uint flags, uint mask, out uint attributes);
        void GetCount(out uint count);
        void GetItemAt(uint index, out IShellItem item);
        void EnumItems(out IntPtr items);
    }
}
