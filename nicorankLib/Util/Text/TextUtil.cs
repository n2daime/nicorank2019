using Microsoft.VisualBasic.FileIO;
using nicorankLib.Analyze.model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Util.Text
{
    public class TextUtil:IDisposable
    {

        object rockObject = new object();

        #region static関数

        /// <summary>
        /// Unicode(UTF-16)でもShift-JISテキストでも、Unicode文字列として読み取れる関数
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="txt"></param>
        /// <returns></returns>
        public static bool ReadText(string filepath, out string txt)
        {
            txt = string.Empty;
            try
            {
                //ファイルを開く
                var fs = new System.IO.FileStream(
                    filepath,
                    System.IO.FileMode.Open,
                    System.IO.FileAccess.Read);

                //ファイルを読み込むバイト型配列を作成する
                var bs = new byte[fs.Length];
                //ファイルの内容をすべて読み込む
                fs.Read(bs, 0, bs.Length);
                //閉じる
                fs.Close();

                //文字コードの判別
                var encoding = TextUtil.GetEnCoding(bs);
                if (encoding == null)
                {
                    var errLog = ErrLog.GetInstance();
                    errLog.Write($"{filepath}読み込みでエラー発生。(TextUtil::ReadText)");
                    errLog.Write("文字コードが判別できません。");
                    return false;
                }
                else
                {
                    txt = encoding.GetString(bs);
                    if(txt.Length > 0 && txt[0] == 0xfeff)
                    {
                        txt = txt.Substring(1);
                    }
                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{filepath}読み込みでエラー発生(TextUtil::ReadText)");
                errLog.Write(ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 文字列の数字変換
        /// </summary>
        /// <param name="baseString"></param>
        /// <param name="lineIdx"></param>
        /// <param name="colIdx"></param>
        /// <returns></returns>
        protected static int nicorank_stoi(string baseString, long lineIdx, long colIdx)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(baseString) && baseString != "--")
                {
                    return int.Parse(baseString);
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($" {lineIdx} 行目 {colIdx}列目[{baseString}]の数字変換でエラー発生(CTextUtil::nicorank_stoi)");
                errLog.Write(ex);
                return 0;
            }
        }

        /// <summary>
        /// CSVファイルを読み込む（カラム名から動的に検出。新旧両対応）
        /// </summary>
        /// <param name="csvpath"></param>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        public static bool ReadCsv(string csvpath, out List<Ranking> rankingList)
        {

            rankingList = new List<Ranking>();
            try
            {
                if (!CsvUtil.Read(csvpath, out List<string[]> rowDataList))
                {
                    var Log = ErrLog.GetInstance();
                    Log.Write($"{csvpath}を開けませんでした。(CTextUtil::ReadCsv)");
                    return false;
                }

                if (rowDataList.Count < 1)
                {
                    //空データ
                    return true;
                }

                //ヘッダーからカラム名→列番号を動的に検出する
                var headerIndex = new Dictionary<string, int>();
                var headerRow = rowDataList[0];
                for (int i = 0; i < headerRow.Length; i++)
                {
                    var colName = headerRow[i]?.Trim();
                    if (!string.IsNullOrEmpty(colName) && !headerIndex.ContainsKey(colName))
                    {
                        headerIndex[colName] = i;
                    }
                }

                //ヘッダーを除く
                rankingList.Capacity = rowDataList.Count - 1;

                //読み込んだ行数をカウント
                long lineIdx = 0;
                foreach (string[] cols in rowDataList)
                {
                    try
                    {
                        if (lineIdx == 0)
                        {
                            //ヘッダーは無視
                        }
                        else
                        {
                            Ranking wRank = new Ranking();

                            //カラム名から値を取得する。列がなければnullを返す
                            Func<string, string> getCol = (colName) =>
                            {
                                if (headerIndex.TryGetValue(colName, out int idx) && idx < cols.Length)
                                {
                                    return cols[idx];
                                }
                                return null;
                            };
                            //数値カラムの取得（列なし→既定値0。総合ランクの空→9999999は下記で判定）
                            Func<string, int> getInt = (colName) =>
                            {
                                var strRow = getCol(colName);
                                if (strRow == null)
                                {
                                    return 0;
                                }
                                int colIdxForLog = headerIndex.TryGetValue(colName, out int idx) ? idx : -1;
                                return nicorank_stoi(strRow, lineIdx, colIdxForLog);
                            };

                            //"ID"
                            wRank.ID = getCol("ID") ?? string.Empty;

                            //"投稿日" 2017年10月15日 20：00：00 投稿
                            var dateStr = getCol("投稿日");
                            if (!string.IsNullOrEmpty(dateStr))
                            {
                                try
                                {
                                    var workStrTime = RegLib.RegExpRep(dateStr, @"([:：\s])|(投稿)", "");
                                    wRank.Date = DateTime.ParseExact(workStrTime, "yyyy年MM月dd日HHmmss", null);
                                }
                                catch (Exception)
                                {
                                }
                            }

                            //"タイトル"
                            wRank.Title = getCol("タイトル") ?? string.Empty;

                            //"再生時間"
                            wRank.PlayTime = getCol("再生時間") ?? string.Empty;

                            //"総合ランク"
                            var rankTotalStr = getCol("総合ランク");
                            if (rankTotalStr == null || string.IsNullOrWhiteSpace(rankTotalStr))
                            {
                                wRank.RankTotal = 9999999;
                            }
                            else
                            {
                                int colIdxForLog = headerIndex.TryGetValue("総合ランク", out int idx) ? idx : -1;
                                wRank.RankTotal = nicorank_stoi(rankTotalStr, lineIdx, colIdxForLog);
                            }

                            //"ポイント"
                            if (headerIndex.ContainsKey("ポイント"))
                            {
                                wRank.PointTotal = getInt("ポイント");
                            }

                            //"カテゴリランク"
                            wRank.RankCategory = getInt("カテゴリランク");

                            //"カテゴリ"
                            wRank.Category = getCol("カテゴリ") ?? string.Empty;

                            //"再生ランク"
                            wRank.RankPlay = getInt("再生ランク");

                            //"再生数"
                            wRank.CountPlay = getInt("再生数");

                            //"コメントランク"
                            wRank.RankComment = getInt("コメントランク");

                            //"コメント数"
                            wRank.CountComment = getInt("コメント数");

                            //"マイリストランク"
                            wRank.RankMyList = getInt("マイリストランク");

                            //"登録数"
                            wRank.CountMyList = getInt("登録数");

                            //"いいねランク"
                            wRank.RankLike = getInt("いいねランク");

                            //"いいね数"
                            wRank.CountLike = getInt("いいね数");

                            //"前回ランク"
                            var lastRankStr = getCol("前回ランク");
                            if (lastRankStr != null)
                            {
                                //第8位とかの表記になってるので、数字だけを抽出する
                                var parseStr = RegLib.RegExpRep(lastRankStr, @"[^\d]*", "");
                                int colIdxForLog = headerIndex.TryGetValue("前回ランク", out int idx) ? idx : -1;
                                wRank.LastRank = nicorank_stoi(parseStr, lineIdx, colIdxForLog);
                            }

                            //"前回ポイント"
                            if (headerIndex.ContainsKey("前回ポイント"))
                            {
                                wRank.LastPoint = getInt("前回ポイント");
                            }

                            //"ユーザーID"
                            wRank.UserID = getCol("ユーザーID") ?? string.Empty;

                            //"ユーザー名"
                            wRank.UserName = getCol("ユーザー名") ?? string.Empty;

                            //"ユーザーアイコン"（旧名"ユーザーアイコンファイル"も許容）
                            var userIcon = getCol("ユーザーアイコン");
                            if (userIcon == null)
                            {
                                userIcon = getCol("ユーザーアイコンファイル");
                            }
                            wRank.UserImageURL = userIcon ?? string.Empty;

                            //"マイリストポイント"（新旧CSVはポイント列と併せて持つ前提。ポイント列なし時は初回CalcPointで再計算値に置き換わる）
                            if (headerIndex.ContainsKey("マイリストポイント"))
                            {
                                wRank.PointMyList = getInt("マイリストポイント");
                            }

                            //"人気のタグ"（Option。なければ空リスト、あればカンマ区切り）
                            wRank.FavoriteTags = new List<string>();
                            var tagsStr = getCol("人気のタグ");
                            if (!string.IsNullOrWhiteSpace(tagsStr))
                            {
                                foreach (var tag in tagsStr.Split(','))
                                {
                                    var workTag = tag?.Trim();
                                    if (!string.IsNullOrEmpty(workTag) && !wRank.FavoriteTags.Contains(workTag))
                                    {
                                        wRank.FavoriteTags.Add(workTag);
                                    }
                                }
                            }

                            //読み取り不要な列は無視する:
                            // "運営ポイントランク","運営ポイント"（情報が古すぎる）
                            // "コメント補正","コメントポイント","マイリスト補正","再生補正","再生ポイント","いいねポイント","ポイント全体補正"（再計算するため）
                            // "動画URL","サムネイルURL","補正値"（旧列。互換のため無視）

                            rankingList.Add(wRank);
                        }
                    }
                    catch (Exception ex)
                    {
                        var errLog = ErrLog.GetInstance();
                        errLog.Write($"{csvpath} {lineIdx} 行目読み込みでエラー発生(TextUtil::ReadCsv)");
                        errLog.Write(ex);
                        return false;
                    }
                    lineIdx++;
                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{csvpath} 読み込みでエラー発生(TextUtil::ReadCsv)");
                errLog.Write(ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 動画IDで検索できる形でCSVファイルを読み込む
        /// </summary>
        /// <param name="csvpath"></param>
        /// <param name="rankingmap"></param>
        /// <returns></returns>
        public static bool ReadCsv(string csvpath, out Dictionary<string, Ranking> rankingmap)
        {

            if (!TextUtil.ReadCsv(csvpath, out List<Ranking> rankingList))
            {
                rankingmap = new Dictionary<string, Ranking>();
                return false;
            }
            rankingmap = new Dictionary<string, Ranking>(rankingList.Count);

            foreach (var ranking in rankingList)
            {
                rankingmap[ranking.ID] = ranking;
            }
            return true;
        }

        /// <summary>
        /// カレントフォルダにあるファイルの一覧を取得する
        /// </summary>
        /// <param name="jyoken"></param>
        /// <param name="filterList"></param>
        /// <param name="resultList"></param>
        /// <returns></returns>
        public static bool SearchCurrentFile(string jyoken, out List<string> filterList, out List<string> resultList)
        {
            throw null;
        }


        #endregion


        protected StreamWriter streamWriter = null;

        /// <summary>
        /// 書き込みようにファイルを開く
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="isUnicode"></param>
        /// <param name="isOverwrite"></param>
        /// <returns></returns>
        public bool WriteOpen(string filepath, bool isUnicode, bool isOverwrite = true)
        {
            lock (rockObject)
            {
                this.WriteClose();

                try
                {
                    var writeEncoding = isUnicode ? Encoding.UTF8 : Encoding.GetEncoding("shift_jis");

                    if (!isOverwrite && File.Exists(filepath))
                    {
                        //追記モードの時、最後の文字が改行で無い場合は改行を追加する
                        using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                        {
                            if (fs.Length > 0)
                            {
                                //最後の文字にSeek
                                fs.Seek(-1, SeekOrigin.End);
                                char lastChar = (char)fs.ReadByte();
                                if (lastChar != '\n' && lastChar != '\r')
                                {
                                    var byteData = writeEncoding.GetBytes($"\r\n");
                                    fs.Write(byteData, 0, byteData.Length);
                                }
                            }
                            fs.Close();
                        }
                    }

                    this.streamWriter = new StreamWriter(filepath, !isOverwrite, writeEncoding);
                }
                catch (Exception)
                {
                    StatusLog.Write($"{filepath} を書き込み用に開けませんでした。\n");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// テキストを書き込む
        /// </summary>
        /// <param name="str"></param>
        public void WriteText(string str)
        {
            lock (rockObject)
            {
                this.streamWriter.Write(str);
            }
        }

        /// <summary>
        /// テキストを改行コードつきで書き込む
        /// </summary>
        /// <param name="str"></param>
        public void WriteLine(string str)
        {
            lock (rockObject)
            {
                this.streamWriter.WriteLine(str);
            }
        }

        /// <summary>
        /// キャッシュではなく一端書き込む
        /// </summary>
        /// <param name="str"></param>
        public void WriteFlush()
        {
            lock (rockObject)
            {
                this.streamWriter.Flush();
            }
        }

        /// <summary>
        /// 書き込みしているファイルを閉じる
        /// </summary>
        /// <returns></returns>
        public bool WriteClose()
        {
            lock (rockObject)
            {
                if (this.streamWriter != null)
                {
                    try
                    {
                        this.streamWriter.Close();
                    }
                    catch
                    {

                    }
                    this.streamWriter = null;
                }
            }
            return true;
        }


        /// <summary>
        /// 文字コードを判別する
        /// </summary>
        /// <remarks>
        /// Jcode.pmのgetcodeメソッドを移植したものです。
        /// Jcode.pm(http://openlab.ring.gr.jp/Jcode/index-j.html)
        /// Jcode.pmの著作権情報
        /// Copyright 1999-2005 Dan Kogai <dankogai@dan.co.jp>
        /// This library is free software; you can redistribute it and/or modify it
        ///  under the same terms as Perl itself.
        /// </remarks>
        /// <param name="bytes">文字コードを調べるデータ</param>
        /// <returns>適当と思われるEncodingオブジェクト。
        /// 判断できなかった時はnull。</returns>
        protected static Encoding GetEnCoding(byte[] bytes)
        {
            const byte bEscape = 0x1B;
            const byte bAt = 0x40;
            const byte bDollar = 0x24;
            const byte bAnd = 0x26;
            const byte bOpen = 0x28;    //'('
            const byte bB = 0x42;
            const byte bD = 0x44;
            const byte bJ = 0x4A;
            const byte bI = 0x49;

            int len = bytes.Length;
            byte b1, b2, b3, b4;

            //Encode::is_utf8 は無視

            bool isBinary = false;
            for (int i = 0; i < len; i++)
            {
                b1 = bytes[i];
                if (b1 <= 0x06 || b1 == 0x7F || b1 == 0xFF)
                {
                    //'binary'
                    isBinary = true;
                    if (b1 == 0x00 && i < len - 1 && bytes[i + 1] <= 0x7F)
                    {
                        //smells like raw unicode
                        return System.Text.Encoding.Unicode;
                    }
                }
            }
            if (isBinary)
            {
                return null;
            }

            //not Japanese
            bool notJapanese = true;
            for (int i = 0; i < len; i++)
            {
                b1 = bytes[i];
                if (b1 == bEscape || 0x80 <= b1)
                {
                    notJapanese = false;
                    break;
                }
            }
            if (notJapanese)
            {
                return System.Text.Encoding.ASCII;
            }

            for (int i = 0; i < len - 2; i++)
            {
                b1 = bytes[i];
                b2 = bytes[i + 1];
                b3 = bytes[i + 2];

                if (b1 == bEscape)
                {
                    if (b2 == bDollar && b3 == bAt)
                    {
                        //JIS_0208 1978
                        //JIS
                        return System.Text.Encoding.GetEncoding(50220);
                    }
                    else if (b2 == bDollar && b3 == bB)
                    {
                        //JIS_0208 1983
                        //JIS
                        return System.Text.Encoding.GetEncoding(50220);
                    }
                    else if (b2 == bOpen && (b3 == bB || b3 == bJ))
                    {
                        //JIS_ASC
                        //JIS
                        return System.Text.Encoding.GetEncoding(50220);
                    }
                    else if (b2 == bOpen && b3 == bI)
                    {
                        //JIS_KANA
                        //JIS
                        return System.Text.Encoding.GetEncoding(50220);
                    }
                    if (i < len - 3)
                    {
                        b4 = bytes[i + 3];
                        if (b2 == bDollar && b3 == bOpen && b4 == bD)
                        {
                            //JIS_0212
                            //JIS
                            return System.Text.Encoding.GetEncoding(50220);
                        }
                        if (i < len - 5 &&
                            b2 == bAnd && b3 == bAt && b4 == bEscape &&
                            bytes[i + 4] == bDollar && bytes[i + 5] == bB)
                        {
                            //JIS_0208 1990
                            //JIS
                            return System.Text.Encoding.GetEncoding(50220);
                        }
                    }
                }
            }

            //should be euc|sjis|utf8
            //use of (?:) by Hiroki Ohzaki <ohzaki@iod.ricoh.co.jp>
            int sjis = 0;
            int euc = 0;
            int utf8 = 0;
            for (int i = 0; i < len - 1; i++)
            {
                b1 = bytes[i];
                b2 = bytes[i + 1];
                if (((0x81 <= b1 && b1 <= 0x9F) || (0xE0 <= b1 && b1 <= 0xFC)) &&
                    ((0x40 <= b2 && b2 <= 0x7E) || (0x80 <= b2 && b2 <= 0xFC)))
                {
                    //SJIS_C
                    sjis += 2;
                    i++;
                }
            }
            for (int i = 0; i < len - 1; i++)
            {
                b1 = bytes[i];
                b2 = bytes[i + 1];
                if (((0xA1 <= b1 && b1 <= 0xFE) && (0xA1 <= b2 && b2 <= 0xFE)) ||
                    (b1 == 0x8E && (0xA1 <= b2 && b2 <= 0xDF)))
                {
                    //EUC_C
                    //EUC_KANA
                    euc += 2;
                    i++;
                }
                else if (i < len - 2)
                {
                    b3 = bytes[i + 2];
                    if (b1 == 0x8F && (0xA1 <= b2 && b2 <= 0xFE) &&
                        (0xA1 <= b3 && b3 <= 0xFE))
                    {
                        //EUC_0212
                        euc += 3;
                        i += 2;
                    }
                }
            }
            for (int i = 0; i < len - 1; i++)
            {
                b1 = bytes[i];
                b2 = bytes[i + 1];
                if ((0xC0 <= b1 && b1 <= 0xDF) && (0x80 <= b2 && b2 <= 0xBF))
                {
                    //UTF8
                    utf8 += 2;
                    i++;
                }
                else if (i < len - 2)
                {
                    b3 = bytes[i + 2];
                    if ((0xE0 <= b1 && b1 <= 0xEF) && (0x80 <= b2 && b2 <= 0xBF) &&
                        (0x80 <= b3 && b3 <= 0xBF))
                    {
                        //UTF8
                        utf8 += 3;
                        i += 2;
                    }
                }
            }
            //M. Takahashi's suggestion
            //utf8 += utf8 / 2;

            System.Diagnostics.Debug.WriteLine(
                string.Format("sjis = {0}, euc = {1}, utf8 = {2}", sjis, euc, utf8));
            if (euc > sjis && euc > utf8)
            {
                //EUC
                return System.Text.Encoding.GetEncoding(51932);
            }
            else if (sjis > euc && sjis > utf8)
            {
                //SJIS
                return System.Text.Encoding.GetEncoding(932);
            }
            else if (utf8 > euc && utf8 > sjis)
            {
                //UTF8
                return System.Text.Encoding.UTF8;
            }

            return null;
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    this.WriteClose();
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        ~TextUtil()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(false);
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
             GC.SuppressFinalize(this);
        }
        #endregion
    }
}
