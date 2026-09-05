namespace nicorankLib.Util
{
    /// <summary>
    /// 集計開始時のDB更新確認に応じるDB担当クラスのIF。
    /// 実処理は各DB担当クラスが持ち、司令塔（DbMigrationCoordinator）が指示する。
    /// </summary>
    public interface IDbMigratable
    {
        /// <summary>
        /// 対象DBファイルパス（DB定数）。
        /// </summary>
        string TargetDb { get; }

        /// <summary>
        /// DBを最新の構成に更新する。冪等であること。
        /// </summary>
        /// <returns>正常終了時true、失敗時false</returns>
        bool EnsureMigrated();
    }
}
