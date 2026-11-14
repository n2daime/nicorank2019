using nicorankLib.Util;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace nicorank2019.Util
{
    /// <summary>
    /// 保存先フォルダを選ぶダイアログ（Issue #56）。
    ///
    /// 既存の <see cref="FolderBrowserDialog"/> は Windows の古いツリー表示で、
    /// 目的のフォルダまで階層をたどる必要があり使いにくい。ここでは Windows Vista 以降の
    /// 標準ダイアログ IFileOpenDialog に FOS_PICKFOLDERS を指定し、エクスプローラーと同じ
    /// 操作感の「フォルダーの選択」を出す。追加の NuGet パッケージは使わない。
    ///
    /// COM 呼び出しに失敗した環境（将来の OS 変更等）でも画像DL自体を止めないよう、
    /// 例外時は従来の FolderBrowserDialog にフォールバックする。
    /// </summary>
    internal static class FolderPicker
    {
        /// <summary>
        /// フォルダ選択ダイアログを表示する。キャンセル時は null。
        /// </summary>
        public static string Show(IWin32Window owner, string title, string initialFolder)
        {
            try
            {
                return ShowModern(owner, title, initialFolder);
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return ShowLegacy(owner, title, initialFolder);
            }
        }

        private static string ShowModern(IWin32Window owner, string title, string initialFolder)
        {
            var dialog = (IFileDialog)new FileOpenDialog();
            try
            {
                dialog.GetOptions(out FILEOPENDIALOGOPTIONS options);
                // フォルダを選ばせ、実ファイルシステム上の存在するパスだけを返す。
                options |= FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS
                    | FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM
                    | FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST;
                dialog.SetOptions(options);

                if (!string.IsNullOrWhiteSpace(title))
                {
                    dialog.SetTitle(title);
                }

                if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
                {
                    IShellItem folder;
                    Guid riid = typeof(IShellItem).GUID;
                    if (SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, ref riid, out folder) == 0
                        && folder != null)
                    {
                        try
                        {
                            dialog.SetFolder(folder);
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(folder);
                        }
                    }
                }

                int hr = dialog.Show(owner == null ? IntPtr.Zero : owner.Handle);
                if (hr != 0)
                {
                    // キャンセル（HRESULT_FROM_WIN32(ERROR_CANCELLED)）やエラー。
                    return null;
                }

                IShellItem result;
                dialog.GetResult(out result);
                try
                {
                    string path;
                    result.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out path);
                    return path;
                }
                finally
                {
                    Marshal.ReleaseComObject(result);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(dialog);
            }
        }

        private static string ShowLegacy(IWin32Window owner, string title, string initialFolder)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = title;
                dialog.ShowNewFolderButton = true;
                if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
                {
                    dialog.SelectedPath = initialFolder;
                }
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        [Flags]
        private enum FILEOPENDIALOGOPTIONS : uint
        {
            FOS_PICKFOLDERS = 0x00000020,
            FOS_FORCEFILESYSTEM = 0x00000040,
            FOS_PATHMUSTEXIST = 0x00000800,
        }

        private enum SIGDN : uint
        {
            SIGDN_FILESYSPATH = 0x80058000,
        }

        [ComImport]
        [Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            // IModalWindow
            [PreserveSig]
            int Show(IntPtr parent);

            // IFileDialog（vtable の順序を変えないこと）
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(FILEOPENDIALOGOPTIONS fos);
            void GetOptions(out FILEOPENDIALOGOPTIONS pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
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

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        /// <summary>FileOpenDialog の COM クラス（CLSID）。IFileDialog を実装する。</summary>
        [ComImport]
        [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog
        {
        }
    }
}
