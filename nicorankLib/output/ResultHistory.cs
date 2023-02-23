using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nicorankLib.Analyze.model;
using nicorankLib.Common;
using nicorankLib.Util;

namespace nicorankLib.output
{
    /// <summary>
    /// 集計結果を先週の結果として出力するかどうか
    /// </summary>
    public class ResultHistory : OutputBase
    {

        /// <summary>
        /// DBファイル名
        /// </summary>
        public const string DATASOURCE = DB.NiCORAN_HISTORY;

        /// <summary>
        /// 集計日
        /// </summary>
        protected DateTime syuukeiBi;



        /// <summary>
        /// モード
        /// </summary>
        public EAnalyzeMode Mode { get; protected set; }

        public ResultHistory(EAnalyzeMode mode)
        {
            this.Mode = mode;
        }

        /// <summary>
        /// 集計日を設定する
        /// </summary>
        /// <param name="baseTime"></param>
        public void SetSyuukeiBi(DateTime syuukeiBi)
        {
            this.syuukeiBi = syuukeiBi;
        }

        /// <summary>
        /// 出力する
        /// </summary>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        public override bool Execute(IReadOnlyList<Ranking> rankingList)
        {
            try
            {
                using (var dbCtrl = new SQLiteCtrl())
                {
                    if (!dbCtrl.Open(DATASOURCE))
                    {
                        StatusLog.WriteLine($"{ DATASOURCE }が参照できません。");
                        return false;
                    }
                    using (var aCmd = new SQLiteCommand(dbCtrl.Connection))
                    {
                        try
                        {
                            // トランザクションの開始
                            aCmd.Transaction = dbCtrl.Connection.BeginTransaction();

                            {//DBの更新確認
                                bool isLikeFieldExist = false;
                                aCmd.CommandText = "PRAGMA table_info('LastResult');";
                                using (var reader = aCmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {

                                        if (reader["name"].ToString().Equals("いいね数"))
                                        {
                                            isLikeFieldExist = true;
                                            break;
                                        }

                                    }
                                    reader.Close();
                                }
                                if (isLikeFieldExist == false)
                                {
                                    //いいねフィールドがない→アップデートする
                                    aCmd.CommandText =
                                        "ALTER TABLE History ADD いいね数 INTEGER DEFAULT 0; " +
                                        "ALTER TABLE LastResult ADD いいね数 INT DEFAULT 0;" +
                                        "ALTER TABLE LastResult ADD 累計いいね数 INT DEFAULT 0;";

                                    aCmd.ExecuteNonQuery();
                                }
                            }

                            switch (this.Mode)
                            {
                                case EAnalyzeMode.Weekly:
                                    if (!updateHistory(aCmd, rankingList).Result)
                                    {
                                        StatusLog.WriteLine($"{ DATASOURCE }のHistroyテーブルの更新に失敗しました。エラーログを確認してください");
                                        aCmd.Transaction.Rollback();
                                        return false;
                                    }
                                    break;
                            }
                            aCmd.Parameters.Clear();

                            if (!updateLastResult(aCmd, rankingList).Result)
                            {
                                StatusLog.WriteLine($"{ DATASOURCE }のLastResultテーブルの更新に失敗しました。エラーログを確認してください");

                                aCmd.Transaction.Rollback();
                                return false;
                            }
                            // トランザクションの開始
                            aCmd.Transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            ErrLog.GetInstance().Write(ex);
                            aCmd.Transaction.Rollback();
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// LastResultテーブルを更新する
        /// </summary>
        /// <param name="dbCtrl"></param>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        protected async Task<bool> updateLastResult(SQLiteCommand sqlCmd, IReadOnlyList<Ranking> rankingList)
        {
            try
            {
                //{// 登録前に古いデータを削除する
                //    sqlCmd.CommandText =
                //        @"DELETE FROM LastResult
                //      WHERE 集計日 <= @削除日 and 種別=@種別;";

                //    // 前回よりさらに1週間以上前のデータは不要なので駆除する
                //    var deleteBi = this.syuukeiBi.AddDays(-14);
                //    sqlCmd.Parameters.AddWithValue("@削除日", DateConvert.Time2String(deleteBi, false));
                //    sqlCmd.Parameters.AddWithValue("@種別", this.Mode.ToString());

                //    // 削除実行
                //    await sqlCmd.ExecuteNonQueryAsync();
                //}

                {// すでに登録済みなのかチェックする
                    sqlCmd.CommandText =
                        @"SELECT * FROM LastResult
                          WHERE 集計日 = @集計日 and 種別=@種別
                          LIMIT 1;";

                    sqlCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(this.syuukeiBi, false));
                    sqlCmd.Parameters.AddWithValue("@種別", this.Mode.ToString());

                    using (var reader = sqlCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            reader.Close();
                            //すでに集計済→上書きのため削除
                            sqlCmd.CommandText =
                                @"DELETE FROM LastResult
                                  WHERE 集計日 = @集計日 and 種別=@種別;";


                            await sqlCmd.ExecuteNonQueryAsync();

                            sqlCmd.CommandText =
                            @"DELETE FROM LastResultInfo
                                  WHERE 集計日 = @集計日 and 種別=@種別;";


                            await sqlCmd.ExecuteNonQueryAsync();
                        }
                        reader.Close();
                    }
                }


                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(this.syuukeiBi, false));
                sqlCmd.Parameters.AddWithValue("@種別", this.Mode.ToString());
                sqlCmd.CommandText =
                                @"INSERT INTO LastResult( 
                                '種別' , '集計日' , 'ID' , 
                                'タイトル' ,'総合ランク' , 'ポイント' ,
                                '再生数' , 'コメント数' , 'マイリスト数' ,
                                '累計再生数' , '累計コメント数' , '累計マイリスト数' , 'JSON'  ,'いいね数', '累計いいね数')
                                  VALUES( 
                                @種別 , @集計日 , @ID , 
                                @タイトル ,@総合ランク , @ポイント ,
                                @再生数 , @コメント数 , @マイリスト数 ,
                                @累計再生数 , @累計コメント数 , @累計マイリスト数 , @JSON ,@いいね数, @累計いいね数);";


                foreach (var rank in rankingList)
                {
                    sqlCmd.Parameters.AddWithValue("@ID", rank.ID);
                    sqlCmd.Parameters.AddWithValue("@タイトル", rank.Title);
                    sqlCmd.Parameters.AddWithValue("@総合ランク", rank.RankTotal);
                    sqlCmd.Parameters.AddWithValue("@ポイント", rank.PointTotal);
                    sqlCmd.Parameters.AddWithValue("@再生数", rank.CountPlay);
                    sqlCmd.Parameters.AddWithValue("@コメント数", rank.CountComment);
                    sqlCmd.Parameters.AddWithValue("@マイリスト数", rank.CountMyList);
                    sqlCmd.Parameters.AddWithValue("@累計再生数", rank.CountPlayTotal);
                    sqlCmd.Parameters.AddWithValue("@累計コメント数", rank.CountCommentTotal);
                    sqlCmd.Parameters.AddWithValue("@累計マイリスト数", rank.CountMyListTotal);
                    sqlCmd.Parameters.AddWithValue("@JSON", rank.ToJson());
                    sqlCmd.Parameters.AddWithValue("@いいね数", rank.CountLike);
                    sqlCmd.Parameters.AddWithValue("@累計いいね数", rank.CountLikeTotal);

                    await sqlCmd.ExecuteNonQueryAsync();
                }
                sqlCmd.CommandText =
                @"INSERT INTO LastResultInfo( 
                                '種別' , '集計日' , 'XML'  )
                                  VALUES( 
                                @種別 , @集計日 , @XML );";

                sqlCmd.Parameters.AddWithValue("@XML",Config.GetInstance().GetXMLString());
                await sqlCmd.ExecuteNonQueryAsync();

            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Historyテーブルを更新する
        /// </summary>
        /// <param name="dbCtrl"></param>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        protected async Task<bool> updateHistory(SQLiteCommand sqlCmd, IReadOnlyList<Ranking> rankingList)
        {
            var config = Config.GetInstance();
            try
            {
                // すでに登録済みなのかチェックする
                sqlCmd.CommandText =
                    @"SELECT * FROM History
                 WHERE 集計日 = @集計日
                 LIMIT 1;";

                sqlCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(this.syuukeiBi, false));

                using (var reader = sqlCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        //すでに集計済→上書きのため削除
                        reader.Close();

                        sqlCmd.CommandText =
                            @"DELETE FROM History
                                  WHERE 集計日 = @集計日;";
                        await sqlCmd.ExecuteNonQueryAsync();
                    }
                }

                sqlCmd.CommandText =
                                @"INSERT INTO History( 
                                 '集計日' , 'ID', '総合ランク',
                                 'ポイント' , '再生数' ,'コメント数' , 'マイリスト数','いいね数')
                                  VALUES( 
                                 @集計日 , @ID, @総合ランク,
                                 @ポイント , @再生数 ,@コメント数 , @マイリスト数 , @いいね数)";

                int movieCnt = 0;

                foreach (var rank in rankingList)
                {
                    sqlCmd.Parameters.AddWithValue("@ID", rank.ID);
                    sqlCmd.Parameters.AddWithValue("@総合ランク", rank.RankTotal);
                    sqlCmd.Parameters.AddWithValue("@ポイント", rank.PointTotal);
                    sqlCmd.Parameters.AddWithValue("@再生数", rank.CountPlay);
                    sqlCmd.Parameters.AddWithValue("@コメント数", rank.CountComment);
                    sqlCmd.Parameters.AddWithValue("@マイリスト数", rank.CountMyList);
                    sqlCmd.Parameters.AddWithValue("@いいね数", rank.CountLike);
                    await sqlCmd.ExecuteNonQueryAsync();

                    movieCnt++;
                    if (movieCnt >= config.Rank)
                    {
                        //設定された紹介枠まで登録する（長期枠は考慮しない）
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            return true;
        }

    }
}
