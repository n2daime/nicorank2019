using nicorankLib.Util.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Util
{
    /// <summary>
    /// エラーログを出力するクラス
    /// </summary>
    public class ErrLog
    {
        protected static ErrLog m_Instance = new ErrLog();
        public bool IsWrite { get; protected set; }
        protected TextUtil textUtil = new TextUtil();
        object lockobject = new object();

        /// <summary>
        /// コンストラクタ
        /// </summary>
        protected ErrLog()
        {
            IsWrite = false;
        }

        /// <summary>
        /// デストラクタ
        /// </summary>
        ~ErrLog()
        {
            Close();
        }

        /// <summary>
        /// 共通インスタンスの取得
        /// </summary>
        /// <returns></returns>
        public static ErrLog GetInstance()
        {
            return m_Instance;
        }

        /// <summary>
        /// ログを書き込む
        /// </summary>
        /// <param name="strLog"></param>
        public void Write(string strLog)
        {
            if (string.IsNullOrEmpty(strLog))
            {
                //書き込む内容がないので、何もしない
                return;
            }
            if (!this.IsWrite)
            {
                //初めての書き込み
                if (this.textUtil.WriteOpen("nicorankerr.log", true, false))
                {
                    this.textUtil.WriteText($"---- {DateTime.Now.ToString()} にエラーが発生しました　---- \n");
                    this.IsWrite = true;
                }
                else
                {
                    StatusLog.Write($"nicorankerr.logに書き込みできません。\n");
                    return;
                }
            }
            this.textUtil.WriteLine(strLog);
            this.textUtil.WriteFlush();
        }



        /// <summary>
        /// ログを書き込む
        /// </summary>
        /// <param name="strLog"></param>
        public void Write(Exception ex)
        {
            this.Write($"{ex.ToString()}{ex.Message}\r\n{ex.StackTrace}");
        }
        /// <summary>
        /// ファイルを閉じる
        /// </summary>
        public void Close()
        {
            if (this.IsWrite)
            {
                StatusLog.Write($"エラーが発生しました。nicorankerr.logの内容を確認してください\n");
                this.textUtil.WriteClose();
                IsWrite = false;
                var UI = UIConfig.GetInstance();
                if (!UI.SilentMode)
                {
                    StatusLog.Write($"終了するには何かキーを押してください\n");
                    UIConfig.GetWch();
                }
            }
        }
    }
}
