using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.output;
using nicorankLib.Util;

namespace UnitTest.nicorankLib.Util
{
    /// <summary>
    /// ImageFileNaming のテスト（Issue #56）。
    /// 直接ダウンロードと既存 queue.irv / queue_UserIcon.irv が同じ名前を使うことを保証する。
    /// 既存クラスの GetDownLoadName は protected なので、テスト用の派生クラスで公開して直接比べる。
    /// これにより「ヘルパーだけ正しくて既存出力が違う」という取り違えを防ぐ。
    /// </summary>
    [TestClass]
    public class UnitTestImageFileNaming
    {
        /// <summary>既存の動画キューのファイル名生成を外から呼べるようにするだけの派生クラス。</summary>
        private sealed class TestMovieIcon : ResultImagegetMovieIcon
        {
            public TestMovieIcon() : base("out", "queue.irv", 200)
            {
            }

            public string Name(Ranking rank)
            {
                return GetDownLoadName(rank);
            }
        }

        /// <summary>既存のユーザーアイコンキューのファイル名生成を外から呼べるようにするだけの派生クラス。</summary>
        private sealed class TestUserIcon : ResultImagegetUserIcon
        {
            public TestUserIcon() : base("out", "queue_UserIcon.irv")
            {
            }

            public string Name(Ranking rank)
            {
                return GetDownLoadName(rank);
            }
        }

        [TestMethod]
        public void GetMovieFileName_UsesRankTotal()
        {
            var rank = new Ranking() { RankTotal = 42 };

            Assert.AreEqual("42.jpg", ImageFileNaming.GetMovieFileName(rank));
        }

        [TestMethod]
        public void GetUserIconFileName_UsesUserId()
        {
            var rank = new Ranking() { ID = "sm1", UserID = "12345" };

            Assert.AreEqual("12345.jpg", ImageFileNaming.GetUserIconFileName(rank));
        }

        [TestMethod]
        public void GetUserIconFileName_ChannelPrefixesCh()
        {
            // チャンネル動画（so）はユーザーIDと衝突しないよう ch を付ける（既存踏襲）。
            var rank = new Ranking() { ID = "so40000000", UserID = "12345" };

            Assert.AreEqual("ch12345.jpg", ImageFileNaming.GetUserIconFileName(rank));
        }

        [TestMethod]
        public void GetUserIconFileName_EmptyUserId_ReturnsEmpty()
        {
            var rank = new Ranking() { ID = "sm1", UserID = "" };

            Assert.AreEqual(string.Empty, ImageFileNaming.GetUserIconFileName(rank));
        }

        [TestMethod]
        public void GetFileName_NullRank_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ImageFileNaming.GetMovieFileName(null));
            Assert.AreEqual(string.Empty, ImageFileNaming.GetUserIconFileName(null));
        }

        [TestMethod]
        public void LegacyMovieIcon_UsesSameNameAsHelper()
        {
            var helper = new TestMovieIcon();
            var rank = new Ranking() { RankTotal = 7 };

            Assert.AreEqual(ImageFileNaming.GetMovieFileName(rank), helper.Name(rank));
        }

        [TestMethod]
        public void LegacyUserIcon_UsesSameNameAsHelper()
        {
            var helper = new TestUserIcon();
            var user = new Ranking() { ID = "sm1", UserID = "99", RankTotal = 1 };
            var channel = new Ranking() { ID = "so1", UserID = "99", RankTotal = 2 };
            var empty = new Ranking() { ID = "sm2", UserID = "", RankTotal = 3 };

            Assert.AreEqual(ImageFileNaming.GetUserIconFileName(user), helper.Name(user));
            Assert.AreEqual(ImageFileNaming.GetUserIconFileName(channel), helper.Name(channel));
            Assert.AreEqual(ImageFileNaming.GetUserIconFileName(empty), helper.Name(empty));
        }

        [TestMethod]
        public void LegacyUserIcon_UserInfoRangeOnly()
        {
            // queue_UserIcon.irv は UserInfo.Num（既定1000）の範囲＋カテゴリ1位だけを出す。
            var helper = new TestUserIcon();
            var inRange = new Ranking() { ID = "sm1", UserID = "u1", RankTotal = 1000 };
            var categoryFirst = new Ranking() { ID = "sm2", UserID = "u2", RankTotal = 2000, RankCategory = 1 };
            var outOfRange = new Ranking() { ID = "sm3", UserID = "u3", RankTotal = 2000, RankCategory = 2 };

            Assert.AreEqual("u1.jpg", helper.Name(inRange));
            Assert.AreEqual("u2.jpg", helper.Name(categoryFirst));
            Assert.AreEqual(string.Empty, helper.Name(outOfRange));
        }
    }
}
