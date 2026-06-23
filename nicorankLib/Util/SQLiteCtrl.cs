using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;

namespace nicorankLib.Util
{
    public class SQLiteCtrl : ISQLiteCtrl, IDisposable
    {
        /// <summary>
        /// 実際のDB操作を行うクラス
        /// </summary>
        public SQLiteConnection Connection { get; protected set; }

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
            var builder = new SQLiteConnectionStringBuilder()
            {
                DataSource = this.DataSource,
                // プーリングは環境によって問題を起こすことがあるため無効にする
                Pooling = false,
                // デフォルトで WAL を使用する
                JournalMode = SQLiteJournalModeEnum.Wal,
                // タイムアウトを少し長めに設定
                DefaultTimeout = 30
            };
            this.Connection = new SQLiteConnection(builder.ToString());
            Connection.Open();

            // PRAGMA を明示的に設定して書き込み中の破損リスクを低減する
            try
            {
                using (var cmd = new SQLiteCommand(Connection))
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

            var builder = new SQLiteConnectionStringBuilder()
            {
                DataSource = this.DataSource
            };
            this.Connection = new SQLiteConnection(builder.ToString());
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
