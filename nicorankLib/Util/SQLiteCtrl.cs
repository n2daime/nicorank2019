using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;

namespace nicorankLib.Util
{
    public class SQLiteCtrl : IDisposable
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
                DataSource = this.DataSource
            };
            this.Connection = new SQLiteConnection(builder.ToString());
            Connection.Open();

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
