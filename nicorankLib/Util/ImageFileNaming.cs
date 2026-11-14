using nicorankLib.Analyze.model;
using System;

namespace nicorankLib.Util
{
    /// <summary>
    /// サムネイル・ユーザーアイコンの保存ファイル名を生成する（Issue #56）。
    /// なぜ切り出すか: 既存の queue.irv は Irvine などの外部ツールが後から画像を取得する前提で、
    /// ファイル名は ResultImagegetMovieIcon / ResultImagegetUserIcon が持っている。今回追加する
    /// 直接ダウンロードでも同じ名前で保存する必要があるため、両者が同じ規則を参照できるようにする。
    /// 純粋関数にしてあるのは、ファイル名の一致を単体テストで保証するためである。
    /// </summary>
    public static class ImageFileNaming
    {
        /// <summary>
        /// 動画サムネイルのファイル名。既存の queue.irv と同じ「総合順位.jpg」。
        /// </summary>
        public static string GetMovieFileName(Ranking rank)
        {
            if (rank == null)
            {
                return string.Empty;
            }
            return $"{rank.RankTotal}.jpg";
        }

        /// <summary>
        /// ユーザーアイコンのファイル名。既存の queue_UserIcon.irv と同じ規則。
        /// チャンネル動画（so）はユーザーIDがチャンネルIDと衝突しないよう ch を付ける。
        /// UserID が空の動画は既存出力と同じく対象外（空文字を返す）。
        /// </summary>
        public static string GetUserIconFileName(Ranking rank)
        {
            if (rank == null || string.IsNullOrWhiteSpace(rank.UserID))
            {
                return string.Empty;
            }
            return rank.IsChannel ? $"ch{rank.UserID}.jpg" : $"{rank.UserID}.jpg";
        }
    }
}
