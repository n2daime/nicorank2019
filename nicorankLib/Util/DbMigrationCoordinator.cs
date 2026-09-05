using System;
using System.Collections.Generic;

namespace nicorankLib.Util
{
    /// <summary>
    /// 集計開始時に各DBの更新確認を指示する司令塔。実処理は各DB担当クラス（IDbMigratable）に委譲する。
    /// </summary>
    public class DbMigrationCoordinator
    {
        private readonly IReadOnlyList<IDbMigratable> migratables;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="migratables">更新指示先（実行順）。LogOfficial→NicoranHistoryの順で渡すこと</param>
        public DbMigrationCoordinator(IReadOnlyList<IDbMigratable> migratables)
        {
            this.migratables = migratables ?? throw new ArgumentNullException(nameof(migratables));
        }

        /// <summary>
        /// 集計開始時に全DBの更新確認を行う。1件でも失敗したら中断する。
        /// </summary>
        /// <returns>全件正常終了時true、1件でも失敗時false</returns>
        public bool EnsureAllAtAnalyzeStart()
        {
            foreach (var migratable in migratables)
            {
                StatusLog.WriteLine($"{migratable.TargetDb}の更新確認をしています...");
                if (!migratable.EnsureMigrated())
                {
                    StatusLog.WriteLine($"{migratable.TargetDb}の更新に失敗しました。エラーログを確認してください");
                    return false;
                }
            }
            return true;
        }
    }
}
