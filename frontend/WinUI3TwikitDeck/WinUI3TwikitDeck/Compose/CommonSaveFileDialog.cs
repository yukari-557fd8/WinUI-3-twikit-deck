using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using static WinUI3TwikitDeck.Compose.FileDialogNative;

namespace WinUI3TwikitDeck.Compose;

/// <summary>
/// Win32 コモンダイアログ (IFileSaveDialog) の薄いラッパ。<see cref="CommonOpenFileDialog"/> の保存版。
/// FileSavePicker と違い、決定した時点で 0 バイトのファイルを作らない（書き込みに失敗しても空ファイルが残らない）。
/// </summary>
internal static class CommonSaveFileDialog
{
    private const int HRESULT_CANCELLED = unchecked((int)0x800704C7);

    /// <summary>
    /// ファイルを保存するダイアログを表示する。キャンセル時は null。
    /// 既存のファイルを選ぶと上書きの確認が出る。
    /// </summary>
    /// <param name="defaultFileName">ファイル名の初期値（拡張子付き）。</param>
    /// <param name="defaultExtension">拡張子が無い名前を入力されたときに付ける拡張子（ドット無し。例: json）。</param>
    public static string? Show(
        IntPtr ownerHwnd,
        IReadOnlyList<(string Name, string Spec)> filters,
        string defaultFileName,
        string defaultExtension,
        string? title = null,
        string? initialDirectory = null)
    {
        if (filters is null || filters.Count == 0)
        {
            throw new ArgumentException("At least one filter is required.", nameof(filters));
        }

        var dialog = (IFileSaveDialog)new FileSaveDialogRCW();
        try
        {
            // 既定のオプション（NOCHANGEDIR など）を残したまま足す
            dialog.GetOptions(out var options);
            options |= (uint)FOS.FOS_FORCEFILESYSTEM | (uint)FOS.FOS_PATHMUSTEXIST | (uint)FOS.FOS_OVERWRITEPROMPT;
            dialog.SetOptions(options);

            var specs = new COMDLG_FILTERSPEC[filters.Count];
            for (var i = 0; i < filters.Count; i++)
            {
                specs[i] = new COMDLG_FILTERSPEC
                {
                    pszName = filters[i].Name,
                    pszSpec = filters[i].Spec,
                };
            }

            dialog.SetFileTypes((uint)specs.Length, specs);
            dialog.SetFileTypeIndex(1);
            dialog.SetDefaultExtension(defaultExtension);
            dialog.SetFileName(defaultFileName);

            if (!string.IsNullOrWhiteSpace(title))
            {
                dialog.SetTitle(title);
            }

            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            {
                var shellItemIid = typeof(IShellItem).GUID;
                var hrFolder = SHCreateItemFromParsingName(
                    initialDirectory,
                    IntPtr.Zero,
                    ref shellItemIid,
                    out var folder);
                if (hrFolder == 0 && folder is not null)
                {
                    try
                    {
                        dialog.SetDefaultFolder(folder);
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(folder);
                    }
                }
            }

            var hr = dialog.Show(ownerHwnd);
            if (hr == HRESULT_CANCELLED)
            {
                return null;
            }

            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            dialog.GetResult(out var item);
            try
            {
                item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var psz);
                try
                {
                    var path = Marshal.PtrToStringUni(psz);
                    return string.IsNullOrEmpty(path) ? null : path;
                }
                finally
                {
                    if (psz != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(psz);
                    }
                }
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

    #region COM interop

    [Flags]
    private enum FOS : uint
    {
        FOS_OVERWRITEPROMPT = 0x00000002,
        FOS_FORCEFILESYSTEM = 0x00000040,
        FOS_PATHMUSTEXIST = 0x00000800,
    }

    [ComImport]
    [Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
    private class FileSaveDialogRCW
    {
    }

    /// <summary>
    /// IFileSaveDialog は IFileDialog を継承するため、メソッド順を COM vtable どおりに並べる。
    /// IFileSaveDialog 固有のメソッド（SetSaveAsItem など）は使わず、vtable の末尾にあるため宣言しない。
    /// </summary>
    [ComImport]
    [Guid("84bccd23-5fde-4cdb-aea4-af64b83d78ab")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileSaveDialog
    {
        // IModalWindow
        [PreserveSig]
        int Show(IntPtr parent);

        // IFileDialog
        void SetFileTypes(uint cFileTypes, [MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName(out IntPtr pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
    }

    #endregion
}
