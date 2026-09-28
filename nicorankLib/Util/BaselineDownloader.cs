using nicorankLib.Analyze.model;
using nicorankLib.api;
using nicorankLib.Common;
using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace nicorankLib.Util
{
    /// <summary>
    /// ベースラインDB（LogOfficial／NicoranHistory）の不在時自動取得を担う（Issue #36）。
    /// NAS の Web 公開配下（既定 https://2daime.myds.me/nicorank/baseline/）の baseline.json を最新ポインタとし、
    /// 本地ファイル不在時に限り zip を取得→サイズ・sha256 照合→展開→配置する。
    /// なぜ不在時に限るか：既存環境の DB を勝手に置き換えると蓄積データの上書き事故になるためである。
    /// 取得に失敗しても例外を投げず false を返し、呼び出し側の既存メッセージで中断する（fail-fast 維持）。
    /// 表示は StatusLog、詳細は ErrLog に寄せる（直接 Console に書かない。Issue #43 の作法）。
    /// </summary>
    public class BaselineDownloader
    {
        /// <summary>最新ポインタのファイル名。配布スクリプト（tools/make-baseline.ps1）が自動作成する。</summary>
        public const string ManifestFileName = "baseline.json";

        /// <summary>
        /// 既定の最新ポインタ URL。Config 不在でも動くよう Config.DefaultBaselineManifestUrl と同一値を重複定義する。
        /// 値を変えたら Config 側と配布スクリプトの OutDir 既定も同時更新すること。
        /// </summary>
        public const string DefaultManifestUrl = "https://2daime.myds.me/nicorank/baseline/baseline.json";

        /// <summary>baseline.json 内の1DB分の記述。配布スクリプトの出力キーと一致させる。</summary>
        public class BaselineFileEntry
        {
            [JsonProperty("file")]
            public string File { get; set; }
            [JsonProperty("date")]
            public string Date { get; set; }
            [JsonProperty("size")]
            public long Size { get; set; }
            [JsonProperty("sha256")]
            public string Sha256 { get; set; }
        }

        /// <summary>baseline.json 全体。対象はベースライン2種のみ（ApiXML／Dailylog は含めない）。</summary>
        public class BaselineManifest
        {
            [JsonProperty("logOfficial")]
            public BaselineFileEntry LogOfficial { get; set; }
            [JsonProperty("nicoranHistory")]
            public BaselineFileEntry NicoranHistory { get; set; }
        }

        /// <summary>テキスト取得処理の差し替え口。既定は InternetUtil 経由、単体テストではフェイクを注入する。</summary>
        public delegate bool TextFetch(string url, out string text);

        /// <summary>ファイル取得処理の差し替え口。既定は InternetUtil 経由、単体テストではフェイクを注入する。</summary>
        public delegate bool FileFetch(string url, string localPath);

        private readonly string localBaseDir;
        private readonly TextFetch textFetch;
        private readonly FileFetch fileFetch;
        private readonly string manifestUrlOverride;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="localBaseDir">本地の基準フォルダ。省略時はカレントディレクトリ（DB/ からの相対解決のため）。テストでは一時フォルダを指定する</param>
        /// <param name="textFetch">テキスト取得処理（省略時は InternetUtil.TxtDownLoad）</param>
        /// <param name="fileFetch">ファイル取得処理（省略時は InternetUtil.FileDownLoad）</param>
        /// <param name="manifestUrlOverride">最新ポインタ URL の上書き（テスト用。省略時は Config→既定の順に解決する）</param>
        public BaselineDownloader(string localBaseDir = null, TextFetch textFetch = null, FileFetch fileFetch = null, string manifestUrlOverride = null)
        {
            this.localBaseDir = localBaseDir ?? Directory.GetCurrentDirectory();
            this.textFetch = textFetch ?? DefaultTextFetch;
            this.fileFetch = fileFetch ?? InternetUtil.FileDownLoad;
            this.manifestUrlOverride = manifestUrlOverride;
        }

        private static bool DefaultTextFetch(string url, out string text)
        {
            return InternetUtil.TxtDownLoad(url, out text);
        }

        /// <summary>
        /// 最新ポインタ URL。nicorank.xml の SYSTEM/URL_BASELINE があればそれを使い、なければ既定を使う。
        /// なぜ try/catch か：設定取得に失敗しても既定で動かし、不在時取得の機会を残すためである（NicoApi.ResolveThreadMax と同一の考え方）。
        /// </summary>
        public string ManifestUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(manifestUrlOverride))
                {
                    return manifestUrlOverride;
                }
                try
                {
                    return Config.GetInstance().BaselineManifestUrl;
                }
                catch
                {
                    return DefaultManifestUrl;
                }
            }
        }

        /// <summary>
        /// ベースライン2種の不足を検出したら自動取得する。両方そろっていれば何もせず true を返す。
        /// </summary>
        /// <returns>両 DB が利用可能な状態になれば true、取得・照合・配置のいずれかに失敗したら false</returns>
        public bool EnsureBaseline()
        {
            string logPath = Path.Combine(localBaseDir, DB.LOG_OFFICEIAL);
            string histPath = Path.Combine(localBaseDir, DB.NiCORAN_HISTORY);
            bool logMissing = !File.Exists(logPath);
            bool histMissing = !File.Exists(histPath);
            if (!logMissing && !histMissing)
            {
                return true;
            }
            try
            {
                StatusLog.WriteLine("ベースラインDBの不足を検出しました。配布場所から自動取得します...");
                string manifestUrl = ManifestUrl;
                if (!textFetch(manifestUrl, out string manifestText))
                {
                    StatusLog.WriteLine("ベースラインの一覧を取得できませんでした。ネットワークと配布場所を確認してください");
                    return false;
                }
                BaselineManifest manifest;
                if (!TryParseManifest(manifestText, out manifest))
                {
                    StatusLog.WriteLine("ベースラインの一覧を読み取れませんでした。配布場所の baseline.json を確認してください");
                    return false;
                }
                string baseUrl = manifestUrl.Substring(0, manifestUrl.LastIndexOf('/') + 1);
                bool ok = true;
                if (logMissing)
                {
                    ok &= DownloadAndPlace(baseUrl, manifest.LogOfficial, logPath);
                }
                if (histMissing)
                {
                    ok &= DownloadAndPlace(baseUrl, manifest.NicoranHistory, histPath);
                }
                return ok && File.Exists(logPath) && File.Exists(histPath);
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                StatusLog.WriteLine("ベースラインDBの自動取得に失敗しました。エラーログを確認してください");
                return false;
            }
        }

        /// <summary>
        /// キャッシュ扱いの DB（ApiXML／Dailylog）がなければ空から作る（Issue #36 方針）。
        /// なぜ失敗でも true か：キャッシュの確保失敗を理由に集計全体を止めると、1件の取得失敗が全件失敗に見えるためである。
        /// 除外の判断は入力側に残し、ここでは best-effort で確保だけ行う（Issue #40 と同一の考え方）。
        /// ファイルごとに try/catch を分ける。片方の失敗がもう片方の確保を巻き込まないためである（re-review指摘対応）。
        /// </summary>
        /// <returns>常に true（確保失敗時はログに残して続ける）</returns>
        public bool EnsureCacheFiles()
        {
            try
            {
                EnsureApiXmlFile();
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                StatusLog.WriteLine("動画情報キャッシュの確保に失敗しましたが、集計を続けます");
            }
            try
            {
                EnsureDailylogFile();
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                StatusLog.WriteLine("中間集計キャッシュの確保に失敗しましたが、集計を続けます");
            }
            return true;
        }

        /// <summary>
        /// baseline.json の読み取り。純粋処理のため単体テストで直接検証する。
        /// </summary>
        public static bool TryParseManifest(string text, out BaselineManifest manifest)
        {
            manifest = null;
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }
                var parsed = JsonConvert.DeserializeObject<BaselineManifest>(text);
                if (parsed == null || !IsValidEntry(parsed.LogOfficial) || !IsValidEntry(parsed.NicoranHistory))
                {
                    return false;
                }
                manifest = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidEntry(BaselineFileEntry entry)
        {
            if (entry == null)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(entry.File) || string.IsNullOrWhiteSpace(entry.Date))
            {
                return false;
            }
            // file は zip 内ではなく配置先フォルダ直下に置く単純名でなければならない。
            // パス区切りを許すと一時フォルダ外への書き込みや URL の意図しない解決につながるため拒否する。
            // 配布元は自前 NAS で実害は小さいが、防御が安い箇所のためここで縛る（reviewer指摘対応）。
            if (entry.File.IndexOf('/') >= 0 || entry.File.IndexOf('\\') >= 0)
            {
                return false;
            }
            if (entry.Size <= 0)
            {
                return false;
            }
            // sha256 は必須とする（スクリプト自動作成のため人間負荷にならない）。
            // 64文字の16進数でなければ配布物の取り違えとみなす。
            if (string.IsNullOrWhiteSpace(entry.Sha256) || entry.Sha256.Length != 64)
            {
                return false;
            }
            foreach (char c in entry.Sha256)
            {
                bool isHex = ('0' <= c && c <= '9') || ('a' <= c && c <= 'f') || ('A' <= c && c <= 'F');
                if (!isHex)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// zip1件の取得→照合→展開→配置。公開情報の表示は StatusLog、例外の詳細は ErrLog に残す。
        /// </summary>
        private bool DownloadAndPlace(string baseUrl, BaselineFileEntry entry, string destPath)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "nicorank_baseline_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string zipPath = Path.Combine(tempDir, entry.File);
            try
            {
                StatusLog.WriteLine(string.Format("{0} を取得しています...", entry.File));
                if (!fileFetch(baseUrl + entry.File, zipPath))
                {
                    StatusLog.WriteLine(string.Format("{0} を取得できませんでした。ネットワークと配布場所を確認してください", entry.File));
                    return false;
                }
                if (!File.Exists(zipPath) || new FileInfo(zipPath).Length != entry.Size)
                {
                    StatusLog.WriteLine(string.Format("{0} のサイズが一致しません。再取得してください", entry.File));
                    return false;
                }
                if (!VerifySha256(zipPath, entry.Sha256))
                {
                    StatusLog.WriteLine(string.Format("{0} の検証に失敗しました。再取得してください", entry.File));
                    return false;
                }
                if (!ExtractSingleDb(zipPath, destPath))
                {
                    StatusLog.WriteLine(string.Format("{0} の展開に失敗しました。配布物を確認してください", entry.File));
                    return false;
                }
                StatusLog.WriteLine(string.Format("{0} を配置しました", entry.File));
                return true;
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                StatusLog.WriteLine(string.Format("{0} の配置に失敗しました。エラーログを確認してください", entry.File));
                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); } } catch { }
            }
        }

        /// <summary>
        /// zip 内の .db 1件を配置先へ出す。zip 直下・サブフォルダのいずれでも先頭の .db を使う。
        /// なぜ1件に絞るか：配布スクリプトが DB ごとに分離して zip 化するため、1つの zip に複数の DB が混ざらない前提だからである。
        /// </summary>
        private static bool ExtractSingleDb(string zipPath, string destPath)
        {
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                ZipArchiveEntry dbEntry = null;
                foreach (var e in zip.Entries)
                {
                    if (e.FullName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                    {
                        dbEntry = e;
                        break;
                    }
                }
                if (dbEntry == null)
                {
                    return false;
                }
                string destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                if (File.Exists(destPath))
                {
                    // 競合で既に置かれた場合は上書きしない（蓄積データの保護を優先する）。
                    return true;
                }
                string tempDb = Path.Combine(Path.GetTempPath(), "nicorank_baseline_" + Guid.NewGuid().ToString("N") + ".db");
                try
                {
                    dbEntry.ExtractToFile(tempDb);
                    if (!File.Exists(tempDb) || new FileInfo(tempDb).Length == 0)
                    {
                        return false;
                    }
                    File.Move(tempDb, destPath);
                    return true;
                }
                finally
                {
                    try { if (File.Exists(tempDb)) { File.Delete(tempDb); } } catch { }
                }
            }
        }

        /// <summary>
        /// ファイルの SHA256 照合。純粋処理のため単体テストで直接検証する。
        /// </summary>
        public static bool VerifySha256(string path, string expected)
        {
            try
            {
                if (string.IsNullOrEmpty(expected) || !File.Exists(path))
                {
                    return false;
                }
                using (var sha = SHA256.Create())
                {
                    using (var stream = File.OpenRead(path))
                    {
                        byte[] hash = sha.ComputeHash(stream);
                        string actual = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        return string.Equals(actual, expected.ToLowerInvariant(), StringComparison.Ordinal);
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private void EnsureApiXmlFile()
        {
            // パス定数は DbOptimizer に集約されているものを使い、値の食い違いを起こさない。
            // 変更時は DbOptimizer／NicoApi／ApiXmlCacheImporter／TyukanAnalyze と同時更新すること。
            string path = Path.Combine(localBaseDir, DbOptimizer.ApiXmlDbPath);
            // 新規作成したファイルは失敗時に削除し、次回リトライ可能にする。
            // なぜ削除するか：空の .db だけ残ると次回以降 File.Exists で早期 return し、
            // 表が作られないまま固定化して中間集計・取得が静かに使えなくなるため（reviewer指摘対応）。
            bool created = false;
            try
            {
                if (!File.Exists(path))
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    // SQLiteCtrl.Open は存在しないファイルを開かないため、空ファイルを作ってから開く（SnapShotDB.InitilizeDB と同一の作り方）。
                    File.Create(path).Dispose();
                    created = true;
                }
                // 表確保は毎回実行する（冪等・軽量）。前回中断で空ファイルだけ残った場合の自己回復のためである。
                using (var dbCtrl = new SQLiteCtrl())
                {
                    if (dbCtrl.Open(path))
                    {
                        ApiXmlCacheImporter.EnsureNicovideoThumbTable(dbCtrl);
                        dbCtrl.Close();
                    }
                }
            }
            catch
            {
                // 新規作成時は本体と -wal/-shm を消して次回リトライ可能にする。
                // Open は WAL 化するため異常終了経路で付随ファイルが残り得る。既存ファイルは消さない。
                if (created)
                {
                    try { if (File.Exists(path)) { File.Delete(path); } } catch { }
                    try { if (File.Exists(path + "-wal")) { File.Delete(path + "-wal"); } } catch { }
                    try { if (File.Exists(path + "-shm")) { File.Delete(path + "-shm"); } } catch { }
                }
                throw;
            }
            if (created)
            {
                StatusLog.WriteLine("動画情報キャッシュを新規作成しました");
            }
        }

        private void EnsureDailylogFile()
        {
            string path = Path.Combine(localBaseDir, DbOptimizer.DailylogDbPath);
            bool created = false;
            try
            {
                if (!File.Exists(path))
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Create(path).Dispose();
                    created = true;
                }
                using (var dbCtrl = new SQLiteCtrl())
                {
                    if (dbCtrl.Open(path))
                    {
                        EnsureDailylogTable(dbCtrl);
                        dbCtrl.Close();
                    }
                }
            }
            catch
            {
                if (created)
                {
                    try { if (File.Exists(path)) { File.Delete(path); } } catch { }
                    try { if (File.Exists(path + "-wal")) { File.Delete(path + "-wal"); } } catch { }
                    try { if (File.Exists(path + "-shm")) { File.Delete(path + "-shm"); } } catch { }
                }
                throw;
            }
            if (created)
            {
                StatusLog.WriteLine("中間集計のキャッシュを新規作成しました");
            }
        }

        /// <summary>
        /// Dailylog 表の確保。本番コードに CREATE 経路がないためここで持つ（DbOptimizer が DROP 禁止にしている理由）。
        /// 列構成は中間集計の INSERT 列と一致させること。不一致があると集計時に列不足で失敗する。
        /// </summary>
        private static void EnsureDailylogTable(ISQLiteCtrl dbCtrl)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Dailylog (
                        集計日 INTEGER,
                        ID TEXT,
                        タイトル TEXT,
                        投稿日 INTEGER,
                        再生時間 TEXT,
                        総合順位 INTEGER,
                        ポイント数 INTEGER,
                        カテゴリランク INTEGER,
                        カテゴリ TEXT,
                        人気のタグ TEXT,
                        再生ランク INTEGER,
                        再生数 INTEGER,
                        再生補正 REAL,
                        再生ポイント INTEGER,
                        コメントランク INTEGER,
                        コメント数 INTEGER,
                        コメント補正 REAL,
                        コメントポイント INTEGER,
                        マイリストランク INTEGER,
                        マイリスト数 INTEGER,
                        マイリスト補正 REAL,
                        マイリストポイント INTEGER,
                        いいねランク INTEGER DEFAULT 0,
                        いいね数 INTEGER DEFAULT 0,
                        いいね補正 INTEGER DEFAULT 1,
                        いいねポイント INTEGER DEFAULT 0,
                        イメージパス TEXT
                    )";
                cmd.ExecuteNonQuery();
            }
        }
    }
}
