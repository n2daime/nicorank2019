using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace nicorankLib.Util
{
    public class SQLiteCtrl : ISQLiteCtrl, IDisposable
    {
        static SQLiteCtrl()
        {
            // SQLitePCLRaw.batteries_v2 は自アセンブリの Location を基準に
            // runtimes/{rid}/native/e_sqlite3.dll を LoadLibrary する。
            // Costura で埋め込むと Location が空になり探索が必ず失敗するため、
            // FodyWeavers.xml の ExcludeAssemblies で埋め込み除外し物理 DLL として配置している。
            // 物理配置が正しければここで初期化が成功する。失敗時は診断情報を付けて上位に伝播する。
            try
            {
                Batteries_V2.Init();
            }
            catch (Exception ex)
            {
                var batteryLocation = typeof(SQLitePCL.Batteries_V2).Assembly.Location;
                throw new InvalidOperationException(
                    $"SQLitePCLRaw の初期化に失敗しました: {ex.Message}\n" +
                    $"batteries_v2.dll の場所: {(string.IsNullOrEmpty(batteryLocation) ? "(埋め込み: FodyWeavers.xml の Costura 除外と Private=False を確認してください)" : batteryLocation)}\n" +
                    $"lib\\SQLitePCLRaw.batteries_v2.dll と同じ lib\\runtimes\\win-x64\\native\\e_sqlite3.dll が配置されている必要があります（probing privatePath=\"lib\"）。",
                    ex);
            }
        }
        /// <summary>
        /// 実際のDB操作を行うクラス
        /// </summary>
        public SqliteConnection Connection { get; protected set; }

        /// <summary>
        /// 接続先を開いているかどうか
        /// </summary>
        public bool IsOpen { get; protected set; }

        /// <summary>
        /// 接続先のDBファイルパス
        /// </summary>
        protected string DataSource;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public SQLiteCtrl()
        {
        }

        /// <summary>
        /// デストラクタ
        /// </summary>
        ~SQLiteCtrl()
        {
            Dispose(false);
        }

        /// <summary>
        /// DBに接続する
        /// </summary>
        /// <param name="sDataSource"></param>
        /// <returns></returns>
        public bool Open(string sDataSource)
        {
            if(IsOpen)
            {
                if(sDataSource == this.DataSource)
                {
                    //同じ接続であれば処理不要
                    return true;
                }
                else
                {
                    Close();
                }
            }
            //DBの存在チェック、なければNG
            if (!System.IO.File.Exists(sDataSource)) { return false; }

            this.DataSource = sDataSource;

            //DBの接続処理
            var connectionString = $"Data Source={this.DataSource};Pooling=False;Default Timeout=30";
            this.Connection = new SqliteConnection(connectionString);
            Connection.Open();

            // PRAGMA を明示的に設定して書き込み中の破損リスクを低減する
            try
            {
                using (var cmd = Connection.CreateCommand())
                {
                    // WAL モードと同期設定
                    cmd.CommandText = "PRAGMA journal_mode = WAL;";
                    cmd.ExecuteNonQuery();

                    // WAL と組み合わせてパフォーマンス/安全性を調整
                    cmd.CommandText = "PRAGMA synchronous = NORMAL;";
                    cmd.ExecuteNonQuery();

                    // 一時データはメモリ上に保持（必要に応じて変更）
                    cmd.CommandText = "PRAGMA temp_store = MEMORY;";
                    cmd.ExecuteNonQuery();

                    // 適度なキャッシュサイズを設定（負の値は KB 単位）
                    cmd.CommandText = "PRAGMA cache_size = -8000;";
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // PRAGMA 設定に失敗しても接続自体は開いているため継続する。呼び出し側でログを出す。
            }

            this.IsOpen = true;

            return true;
        }

        /// <summary>
        /// DBから切断する
        /// </summary>
        /// <returns></returns>
        public bool Close()
        {
            if( this.Connection != null)
            {
                try
                {
                    if( this.Connection.State == System.Data.ConnectionState.Open)
                    {
                        Connection.Close();
                        IsOpen = false;
                        Connection = null;
                        GC.SuppressFinalize(this);
                    }
                }
                catch { }
            }
            return true;
        }

        /// <summary>
        /// インメモリDBに接続する（テスト用）
        /// </summary>
        /// <returns></returns>
        public bool OpenInMemory()
        {
            if (IsOpen)
            {
                Close();
            }

            this.DataSource = ":memory:";

            var connectionString = $"Data Source={this.DataSource}";
            this.Connection = new SqliteConnection(connectionString);
            Connection.Open();

            this.IsOpen = true;

            return true;
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (this.Connection != null)
                    {
                        Close();
                        this.Connection = null;
                    }
                    disposedValue = true;
                }
            }
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
