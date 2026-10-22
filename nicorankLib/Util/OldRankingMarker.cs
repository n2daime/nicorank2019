using System;
using System.IO;
using System.Linq;
using System.Text;

namespace nicorankLib.Util
{
    /// <summary>
    /// 空の file_name_list.json を置いた結果（Issue #53）。
    /// </summary>
    public enum OldRankingMarkerWriteResult
    {
        /// <summary>新しく配置した</summary>
        Created,
        /// <summary>既にデータ（JSONファイル）があるので配置しなかった</summary>
        SkippedExistingData,
        /// <summary>書き込みに失敗した</summary>
        Failed,
    }

    /// <summary>
    /// 公式過去ランキングの「データなし（メンテナンス）」を示す空の file_name_list.json を扱う（Issue #53）。
    /// </summary>
    /// <remarks>
    /// なぜ空配列なのか:
    /// nicorank2019 は 404 を「未提供」「Webサーバの設定ミス」「データ削除」のどれとも区別できない。
    /// 404 から自動でメンテナンス日を判定すると、一時的な公開遅れや設定ミスで本来取得できる日を
    /// 永久に欠落させる危険がある。そこで、oldlog が1時間リトライしても全滅したときだけ
    /// 「この日はデータなし」という明示的なマーカー（[]）を置き、nicorank2019 はそれだけを
    /// メンテナンス日として扱う。404 の推測より、プロセスの確定的な失敗の方が根拠として強い。
    /// </remarks>
    public static class OldRankingMarker
    {
        /// <summary>マーカーのファイル名。nicorank2019 が読む file_name_list.json と同じ。</summary>
        public const string FileName = "file_name_list.json";

        /// <summary>マーカーの内容。空配列であること自体が「データなし」の意味を持つ。</summary>
        public const string Content = "[]";

        /// <summary>
        /// 指定した term・日付のフォルダに空の file_name_list.json を配置する。
        /// 既に JSON ファイルがあるフォルダは上書きしない（過去の正常データを壊さないため）。
        /// </summary>
        /// <param name="baseDir">old-ranking の親ディレクトリ（通常は "old-ranking"）</param>
        /// <param name="term">daily / total / weekly / monthly などのフォルダ名</param>
        /// <param name="date">対象日</param>
        /// <param name="path">配置先（または配置しようとした先）のパス</param>
        /// <param name="result">配置結果</param>
        /// <param name="error">失敗時の理由</param>
        /// <returns>新しく配置したときだけ true</returns>
        public static bool TryWrite(
            string baseDir,
            string term,
            DateTime date,
            out string path,
            out OldRankingMarkerWriteResult result,
            out string error)
        {
            path = Path.Combine(baseDir, term, date.ToString("yyyy-MM-dd"), FileName);
            result = OldRankingMarkerWriteResult.Failed;
            error = null;

            try
            {
                var dir = Path.GetDirectoryName(path);

                // 既に何らかの JSON がある日は触らない。
                // マーカーだけの日（再実行でまた失敗した場合）もここに含まれ、二重書き込みにならない。
                if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.json").Any())
                {
                    result = OldRankingMarkerWriteResult.SkippedExistingData;
                    return false;
                }

                Directory.CreateDirectory(dir);
                // BOM なし UTF-8。nicorank2019 は StreamReader で読むため BOM があっても読めるが、
                // 他ツールで中身を確認したときに [] だけが見える方が扱いやすい。
                File.WriteAllText(path, Content, new UTF8Encoding(false));
                result = OldRankingMarkerWriteResult.Created;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
