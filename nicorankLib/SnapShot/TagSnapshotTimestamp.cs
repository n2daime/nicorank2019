using System;
using System.Globalization;

namespace nicorankLib.SnapShot
{
    /// <summary>
    /// タグ検索v2最新値モードのデータ時点ラベル整形（Issue #39）。
    /// 日付部は versionエンドポイントの last_modified をJST化した日付を使い、時刻は 05:00 固定で表示する。
    /// なぜ時刻を固定にするか：last_modified の時刻はスナップショットDBへの反映完了時刻であり、
    /// API仕様上のデータ時点は5:00と記述されているため、反映完了時刻（例：07:08）をそのまま出すと
    /// 「その時刻の値」と誤解される。データ時点である5:00に固定することで誤解を防ぐ。
    /// UI（WinForms）から分離した純粋処理にしているのは、単体テストで日付境界・Z表記を検証するためである。
    /// </summary>
    public static class TagSnapshotTimestamp
    {
        /// <summary>仕様上のデータ時刻（時）。API仕様で5:00のデータと記述されているため固定値で持つ</summary>
        public const int DataHour = 5;
        /// <summary>仕様上のデータ時刻（分）</summary>
        public const int DataMinute = 0;

        /// <summary>
        /// last_modified からラベル文面を組み立てる（例：09/26 05:00 時点のスナップショットで集計）。
        /// </summary>
        /// <param name="lastModified">versionエンドポイントの last_modified パース済み値</param>
        /// <returns>ラベル文面</returns>
        public static string Format(DateTimeOffset lastModified)
        {
            // NASのTZがJSTでない場合でも日付境界がずれないよう、JSTに寄せてから日付を取り出す（#38と同一考え）
            string day = SnapShotVersionChecker.ToJst(lastModified).ToString("MM/dd", CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, "{0} {1:D2}:{2:D2} 時点のスナップショットで集計", day, DataHour, DataMinute);
        }

        /// <summary>
        /// version判定結果からラベル文面の取得を試みる。確認不能（LastModifiedなし）は false を返す。
        /// 呼び出し側は false の場合に件数確認自体を失敗扱いにして集計に進めない（ユーザー回答の「エラーとして中断」）。
        /// </summary>
        public static bool TryFormat(SnapShotVersionResult result, out string label)
        {
            label = null;
            if (result == null || !result.LastModified.HasValue)
            {
                return false;
            }
            label = Format(result.LastModified.Value);
            return true;
        }
    }
}
