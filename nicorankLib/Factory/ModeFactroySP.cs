using nicorankLib.Analyze;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.Json;
using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Option;
using nicorankLib.Analyze.Option.Basic;
using nicorankLib.api;
using nicorankLib.Common;
using nicorankLib.output;
using nicorankLib.Util;
using System;
using System.Collections.Generic;


namespace nicorankLib.Factory
{
    /// <summary>
    /// SP集計モードのファクトリ。
    /// 仮集計（UseKariSyukei）をONにすると、動画IDリストのファイルを使わず
    /// NicoranHistory.db の週刊結果から候補IDを作る（Issue #54）。
    /// </summary>
    public class ModeFactroySP: ModeFactoryWeekly
    {

        public String AnalyzeDB;
        public String BaseDB;
        public String MovieListFile;
        public string LastResultCsvFile;

        /// <summary>
        /// 仮集計かどうか。trueなら動画IDリストをDBから作る。
        /// </summary>
        public bool UseKariSyukei { get; protected set; }

        public void SetInputFile(string analyzeDB, string baseDB, string movieListFile,string lastResultCsvFile, bool useKariSyukei = false)
        {
            AnalyzeDB = analyzeDB;
            BaseDB = baseDB;
            MovieListFile = movieListFile;
            LastResultCsvFile = lastResultCsvFile;
            UseKariSyukei = useKariSyukei;
        }

        public override bool CreateAnalyzer()
        {
            var snapShotSabunReader = new SnapShotSabunReader(
                AnalyzeDB , BaseDB);

            if (!snapShotSabunReader.Open())
            {
                // 開けなかった Reader 自体も破棄する。なぜここで閉じるか:
                // 生成済みの SQLiteCtrl（未接続・fallback 未開）は残っても実害は小さいが、
                // 所有権を Factory が持つ以上、失敗経路でも残さないのが一貫するため。
                snapShotSabunReader.Dispose();
                return false;
            }
            TargetDay = snapShotSabunReader.AnalyzeTime;
            BaseDay = snapShotSabunReader.BaseTime;

            // ランキングのベースは動画IDリストから取得する
            InputBase inputBase;
            if (UseKariSyukei)
            {
                // 仮集計: 基準日以降の週刊結果から候補IDを作る。
                // 基準日は基準日DBの集計日（BaseDay）を使う。SPの集計期間は基準日DBから始まるため、
                // それより前の週から拾ってもスナップショット差分が0で順位に効かない。
                if (!TryCreateKariMovieList(BaseDay, out List<string> movieIds))
                {
                    // 抽出に失敗したときは、ここまでに開いた Reader を破棄して中断する
                    snapShotSabunReader.Dispose();
                    return false;
                }
                inputBase = new SPAnalyze(snapShotSabunReader.AnalyzeTime, movieIds);
            }
            else
            {
                inputBase = new SPAnalyze(snapShotSabunReader.AnalyzeTime , MovieListFile);
            }

            
            //集計に必要なオプションを作成する
            var options = new List<BasicOptionBase>()
            {
                snapShotSabunReader                                 //差分計算
            };
            if (!string.IsNullOrWhiteSpace(LastResultCsvFile))
            {
                options.Add(new LastRankCsvReader(LastResultCsvFile));  //先週の順位
            }
            else if (UseKariSyukei)
            {
                // 仮集計では前回結果CSVを省略できる。黙って前回順位なしになると結果の意味が変わって見えるため明示する
                StatusLog.WriteLine("前回結果CSVが未指定のため、前回順位なしで集計します");
            }
            else
            {
                // 通常のSPは従来どおり（未指定でも Reader を追加する）。挙動を変えないため分岐を分ける
                options.Add(new LastRankCsvReader(LastResultCsvFile));
            }

            Config config = Config.GetInstance();
            //集計後に実行する（ランキング順位などを参照する）オプションを作成する
            var extoptions = new List<IExtOptionBase>()
            {
                new FavoriteTagReader(config.UserNum,BaseDay, TargetDay)           //人気のタグ
            };

            this.RankingAnalyze = new RankingAnalyze(inputBase, options, extoptions);

            return true;
        }

        /// <summary>
        /// 仮集計の候補IDを作る。失敗・0件なら false（集計を中断する）。
        /// なぜ0件で中断するか: 空のまま出力すると空の結果ファイルができ、誤って公開しかねないためである。
        /// </summary>
        protected bool TryCreateKariMovieList(DateTime baseDay, out List<string> movieIds)
        {
            movieIds = null;
            using (var provider = new SPKariMovieListProvider())
            {
                if (!provider.Open())
                {
                    StatusLog.WriteLine($"{DB.NiCORAN_HISTORY}が開けないため、仮集計を中止します");
                    return false;
                }
                if (!provider.TryGetMovieIds(baseDay, SPKariMovieListProvider.DefaultRankLimit, out var result))
                {
                    StatusLog.WriteLine("仮集計の動画ID抽出に失敗しました。エラーログを確認してください");
                    return false;
                }
                if (result.Ids.Count == 0)
                {
                    StatusLog.WriteLine(
                        $"仮集計の対象動画がありません（基準日{DateConvert.Time2String(baseDay, false)}・週刊{result.RankLimit}位以内）");
                    return false;
                }

                StatusLog.WriteLine(
                    $"仮集計: {result.Ids.Count}件を抽出しました（基準日{DateConvert.Time2String(baseDay, false)}・週刊{result.RankLimit}位以内）");
                if (result.LatestWeeklyDay.HasValue)
                {
                    // どの週までの週刊結果が入っているかを見せる。最新週が未集計だとその週の新着が候補に入らないため
                    StatusLog.WriteLine($"参照した週刊結果の最新集計日: {DateConvert.Time2String(result.LatestWeeklyDay.Value, false)}");
                }
                // 近似であることを明示する（ニコランWebの定義は週刊・月刊TOP300。月刊DBは本システムに無い）
                StatusLog.WriteLine("仮集計は手元の週刊結果から作る近似のため、ニコランWebのリストと結果が異なる場合があります");

                movieIds = new List<string>(result.Ids);
                return true;
            }
        }

        public override OutputBase CreateHistory()
        {
            return null;
        }
        public override OutputBase CreateOutputJson_rankDB()
        {
            var output = new ResultJsonRankDB();
            output.SetOutput(OUTPUTDIR, "result_DB登録用(UTF8).json");
            return output;
        }
    }
}
