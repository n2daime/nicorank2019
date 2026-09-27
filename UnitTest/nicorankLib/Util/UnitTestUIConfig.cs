using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;

namespace UnitTest.nicorankLib.Util
{
    /// <summary>
    /// UIConfig.GetWch の回帰テスト（Issue #43）。
    /// 以前は非SilentMode時にConsole.ReadLineを呼ぶ層違反があり、WinForm呼び出し・リダイレクト時にブロック等の不定振る舞いになった。
    /// 現行仕様は常時既定値返却であり、SilentModeの値によらず入力待ちしないことを縛る。
    /// </summary>
    [TestClass]
    public class UnitTestUIConfig
    {
        [TestMethod]
        public void TestGetWch_ReturnsDefault_InSilentMode()
        {
            var ui = UIConfig.GetInstance();
            bool saved = ui.SilentMode;
            try
            {
                ui.SilentMode = true;
                Assert.AreEqual("", UIConfig.GetWch());
                Assert.AreEqual("x", UIConfig.GetWch("x"));
            }
            finally
            {
                ui.SilentMode = saved;
            }
        }

        [TestMethod]
        public void TestGetWch_ReturnsDefault_EvenWhenNonSilentMode()
        {
            // 非SilentModeでも入力待ち（Console.ReadLine）せず既定値を返す。リダイレクト時のブロック再発防止。
            var ui = UIConfig.GetInstance();
            bool saved = ui.SilentMode;
            try
            {
                ui.SilentMode = false;
                Assert.AreEqual("", UIConfig.GetWch());
                Assert.AreEqual("abc", UIConfig.GetWch("abc"));
            }
            finally
            {
                ui.SilentMode = saved;
            }
        }

        [TestMethod]
        public void TestGetWch_DoesNotThrow_WithoutConsole()
        {
            // 受け手なし・リダイレクト等の環境でも例外にせず即時復帰すること。ブロックしないことの間接担保。
            var ui = UIConfig.GetInstance();
            bool saved = ui.SilentMode;
            try
            {
                ui.SilentMode = false;
                string result = UIConfig.GetWch("d");
                Assert.AreEqual("d", result);
            }
            finally
            {
                ui.SilentMode = saved;
            }
        }
    }
}
