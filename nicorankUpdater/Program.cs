using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;

// 分離更新担当（Issue #47）。本体（nicorank2019）が更新を検出→本 exe を起動→本体終了→
// 本 exe が待機・置換・再起動する。実行中の exe は自分を置換できないための分離である。
// nicorankLib を参照しない。理由は、更新対象の lib/ 欠け・版ずれの影響を受けず、
// 単一 exe で動き続ける必要があるためである。表示は Console 直書きとする。
// 理由は、StatusLog の受け手（本体プロセス）が終了済みであり、進捗の届け先がコンソールしかないためである。
namespace nicorankUpdater
{
    static class Program
    {
        /// <summary>本体が書く指示ファイル名（%TEMP% 直下。AppUpdateChecker.TaskFileName と一致させる）。</summary>
        private const string TaskFileName = "nicorank2019_update_task.json";

        /// <summary>対応できる指示ファイルの形式版数の上限。未知の形式なら何もせず旧版のまま残す。</summary>
        private const int SupportedTaskSchemaMax = 1;

        /// <summary>終了コード規約（nicorank_oldlog／SnapShot.Cli と同一：0=成功／2=エラー）。</summary>
        private const int ExitOk = 0;
        private const int ExitError = 2;

        /// <summary>本体プロセスの終了待ちの上限（秒）。居座る本体がある場合は置換しない。実行中 exe の置換はできないため。</summary>
        private const int WaitForExitSeconds = 60;

        /// <summary>zip 取得のリトライ回数。本体側 InternetUtil（20回）より少なくする。updater は無人待機ではなく人間が結果を見るため。</summary>
        private const int DownloadRetryMax = 5;

        static int Main(string[] args)
        {
            string taskPath = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), TaskFileName);
            try
            {
                Console.WriteLine("nicorankUpdater: 更新を開始します");
                if (!Run(taskPath))
                {
                    Console.WriteLine("nicorankUpdater: 更新を完了できませんでした。上記のメッセージを確認してください");
                    return ExitError;
                }
                Console.WriteLine("nicorankUpdater: 更新が完了しました");
                return ExitOk;
            }
            catch (Exception ex)
            {
                Console.WriteLine("nicorankUpdater: 予期しないエラー: " + ex.Message);
                return ExitError;
            }
        }

        private static bool Run(string taskPath)
        {
            Dictionary<string, object> task = ReadTask(taskPath);
            if (task == null)
            {
                Console.WriteLine("nicorankUpdater: 指示ファイルを読み取れません: " + taskPath);
                return false;
            }
            string url = GetString(task, "url");
            string sha256 = GetString(task, "sha256");
            string targetDir = GetString(task, "targetDir");
            string exePath = GetString(task, "exePath");
            int processId = GetInt(task, "processId", 0);
            long size = GetLong(task, "size", 0);
            int schema = GetInt(task, "schema", 0);
            if (schema < 1 || schema > SupportedTaskSchemaMax)
            {
                Console.WriteLine("nicorankUpdater: 未知の指示形式のため中断します");
                return false;
            }
            if (!IsSupportedDownloadUrl(url) || !IsSha256(sha256) || size <= 0)
            {
                Console.WriteLine("nicorankUpdater: 配布情報が不正のため中断します");
                return false;
            }
            if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
            {
                Console.WriteLine("nicorankUpdater: 配置先が見つからないため中断します");
                return false;
            }
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                Console.WriteLine("nicorankUpdater: 本体が見つからないため中断します");
                return false;
            }
            string workDir = Path.Combine(Path.GetTempPath(), "nicorank_update_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);
            try
            {
                string zipPath = Path.Combine(workDir, "nicorank2019.zip");
                Console.WriteLine("nicorankUpdater: 配布を取得しています...");
                if (!Download(url, zipPath))
                {
                    Console.WriteLine("nicorankUpdater: 配布を取得できませんでした");
                    return false;
                }
                if (!VerifySize(zipPath, size) || !VerifySha256(zipPath, sha256))
                {
                    Console.WriteLine("nicorankUpdater: 配布の検証に失敗しました");
                    return false;
                }
                if (!WaitForProcessExit(processId))
                {
                    Console.WriteLine("nicorankUpdater: 本体の終了を確認できないため中断します");
                    return false;
                }
                string backupDir = BackupCurrent(targetDir, exePath);
                if (backupDir == null)
                {
                    Console.WriteLine("nicorankUpdater: 旧版の退避に失敗したため中断します");
                    return false;
                }
                Console.WriteLine("nicorankUpdater: 旧版を退避しました: " + backupDir);
                if (!ExtractOver(zipPath, targetDir))
                {
                    // 部分展開では新旧混成が残り、今回の事故（lib/ の部分欠け・版ずれ）を再生産し得る。
                    // 退避先からの自動復元を試み、手動復元を利用者に求めない。
                    bool restored = RestoreBackup(backupDir, targetDir, exePath);
                    if (restored)
                    {
                        Console.WriteLine("nicorankUpdater: 置換に失敗したため旧版に戻しました。旧版のまま使えます");
                    }
                    else
                    {
                        Console.WriteLine("nicorankUpdater: 置換に失敗し、自動復元もできませんでした。退避先から手動で戻してください: " + backupDir);
                    }
                    return false;
                }
                try { if (File.Exists(taskPath)) { File.Delete(taskPath); } } catch { }
                Console.WriteLine("nicorankUpdater: 置換が完了しました。再起動します...");
                if (!RestartApp(exePath))
                {
                    Console.WriteLine("nicorankUpdater: 置換は完了しましたが再起動に失敗しました。手動で起動してください: " + exePath);
                    return false;
                }
                return true;
            }
            finally
            {
                try { if (Directory.Exists(workDir)) { Directory.Delete(workDir, true); } } catch { }
            }
        }

        private static Dictionary<string, object> ReadTask(string taskPath)
        {
            try
            {
                if (!File.Exists(taskPath))
                {
                    return null;
                }
                var serializer = new JavaScriptSerializer();
                return serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(taskPath));
            }
            catch
            {
                return null;
            }
        }

        private static string GetString(Dictionary<string, object> task, string key)
        {
            object value;
            if (!task.TryGetValue(key, out value) || value == null)
            {
                return null;
            }
            return value.ToString().Trim();
        }

        private static int GetInt(Dictionary<string, object> task, string key, int fallback)
        {
            object value;
            if (!task.TryGetValue(key, out value) || value == null)
            {
                return fallback;
            }
            int parsed;
            if (int.TryParse(value.ToString(), out parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private static long GetLong(Dictionary<string, object> task, string key, long fallback)
        {
            object value;
            if (!task.TryGetValue(key, out value) || value == null)
            {
                return fallback;
            }
            long parsed;
            if (long.TryParse(value.ToString(), out parsed))
            {
                return parsed;
            }
            return fallback;
        }

        /// <summary>
        /// 取得可能な URL かを判定する。http／https に加え file を許す。
        /// file を許すのは、NAS・GitHub 配置前のローカル検証（version.json と zip を手元に置く通しテスト）のため。
        /// 配布元が書く値であり信頼モデルは変わらず、size・sha256 照合も適用される。
        /// </summary>
        private static bool IsSupportedDownloadUrl(string url)
        {
            Uri uri;
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return false;
            }
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFile;
        }

        private static bool IsSha256(string sha256)
        {
            if (string.IsNullOrEmpty(sha256) || sha256.Trim().Length != 64)
            {
                return false;
            }
            foreach (char c in sha256.Trim())
            {
                bool isHex = ('0' <= c && c <= '9') || ('a' <= c && c <= 'f') || ('A' <= c && c <= 'F');
                if (!isHex)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Download(string url, string zipPath)
        {
            for (int attempt = 0; attempt < DownloadRetryMax; attempt++)
            {
                try
                {
                    using (var web = new WebClient())
                    {
                        web.Headers.Add("User-Agent", "WeeklyNicoranProgram");
                        web.DownloadFile(url, zipPath);
                    }
                    return File.Exists(zipPath);
                }
                catch
                {
                    try { if (File.Exists(zipPath)) { File.Delete(zipPath); } } catch { }
                    Thread.Sleep(1000 * (attempt + 1));
                }
            }
            return false;
        }

        private static bool VerifySize(string zipPath, long expected)
        {
            try
            {
                return File.Exists(zipPath) && new FileInfo(zipPath).Length == expected;
            }
            catch
            {
                return false;
            }
        }

        private static bool VerifySha256(string zipPath, string expected)
        {
            try
            {
                using (var sha = SHA256.Create())
                {
                    using (var stream = File.OpenRead(zipPath))
                    {
                        string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                        return string.Equals(actual, expected.Trim().ToLowerInvariant(), StringComparison.Ordinal);
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool WaitForProcessExit(int processId)
        {
            if (processId <= 0)
            {
                return true;
            }
            try
            {
                Process process = Process.GetProcessById(processId);
                // 本体は updater 起動後に終了するため、 blocking 待ちではなく上限付きポーリングにする。
                // WaitForExit(無制限) は本体が居座ると無限停止になるため使わない。
                for (int waited = 0; waited < WaitForExitSeconds; waited++)
                {
                    if (process.HasExited)
                    {
                        return true;
                    }
                    Thread.Sleep(1000);
                }
                process.Refresh();
                return process.HasExited;
            }
            catch (ArgumentException)
            {
                // 既に存在しないプロセスIDは終了済みとみなす。
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 置換前に旧版を退避する。対象は exe・config・lib/ 一式とし、部分欠けを構造的に起こせなくするため置換側と一致させる。
        /// 退避先は配置先直下の backup/日時/ とする（#45 の DB 退避と同型）。
        /// </summary>
        /// <returns>退避先。失敗時は null（この場合は置換に進まない）</returns>
        private static string BackupCurrent(string targetDir, string exePath)
        {
            try
            {
                string backupDir = Path.Combine(targetDir, "backup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(backupDir);
                File.Copy(exePath, Path.Combine(backupDir, Path.GetFileName(exePath)), true);
                string configPath = exePath + ".config";
                if (File.Exists(configPath))
                {
                    File.Copy(configPath, Path.Combine(backupDir, Path.GetFileName(configPath)), true);
                }
                string libDir = Path.Combine(targetDir, "lib");
                if (Directory.Exists(libDir))
                {
                    CopyDirectory(libDir, Path.Combine(backupDir, "lib"));
                }
                return backupDir;
            }
            catch
            {
                return null;
            }
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            }
            foreach (string dir in Directory.GetDirectories(source))
            {
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
            }
        }

        /// <summary>
        /// 置換失敗時に退避先から旧版を戻す。部分展開の新旧混成を残さないためである。
        /// 復元自体も失敗し得るため、戻り値で成否を返し、失敗時は退避先の手動復元を案内する。
        /// </summary>
        private static bool RestoreBackup(string backupDir, string targetDir, string exePath)
        {
            try
            {
                File.Copy(Path.Combine(backupDir, Path.GetFileName(exePath)), exePath, true);
                string configName = Path.GetFileName(exePath) + ".config";
                string backupConfig = Path.Combine(backupDir, configName);
                if (File.Exists(backupConfig))
                {
                    File.Copy(backupConfig, Path.Combine(targetDir, configName), true);
                }
                string backupLib = Path.Combine(backupDir, "lib");
                if (Directory.Exists(backupLib))
                {
                    CopyDirectory(backupLib, Path.Combine(targetDir, "lib"));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 置換後の本体を起動する。Process.Start の戻り値を見て、起動失敗を成功と誤認しない。
        /// </summary>
        private static bool RestartApp(string exePath)
        {
            try
            {
                Process started = Process.Start(exePath);
                return started != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// zip の内容で配置先を上書きする。exe・config・lib/ 一式が対象であり、DB・設定本体には触れない。
        /// 自分自身（nicorankUpdater.exe）は置換しない。実行中の自分は置換できないためであり、
        /// updater 自身の更新は別Issueで対応する。ZipSlip 対策として配置先外への展開は拒否する。
        /// </summary>
        private static bool ExtractOver(string zipPath, string targetDir)
        {
            try
            {
                string ownName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
                string targetFull = Path.GetFullPath(targetDir);
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            continue;
                        }
                        if (string.Equals(entry.Name, ownName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        string destPath = Path.GetFullPath(Path.Combine(targetFull, entry.FullName));
                        if (!destPath.StartsWith(targetFull, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                        string destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }
                        entry.ExtractToFile(destPath, true);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
