using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.SnapShot;

namespace UnitTest.nicorankLib.SnapShot
{
    [TestClass]
    public class UnitTestTagConditionParser
    {
        [TestMethod]
        public void SingleTerm_WithoutStar_UsesTagsExact()
        {
            bool ok = TagConditionParser.TryParse("初音ミク", out string json, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"初音ミク\"}",
                json);
        }

        [TestMethod]
        public void SingleTerm_WithStar_UsesTags()
        {
            bool ok = TagConditionParser.TryParse("初音ミク*", out string json, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(
                "{\"type\":\"equal\",\"field\":\"tags\",\"value\":\"初音ミク\"}",
                json);
        }

        [TestMethod]
        public void Spaces_AreAnd_AndBeforeOr()
        {
            bool ok = TagConditionParser.TryParse("A B|C*", out string json, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(
                "{\"type\":\"or\",\"filters\":[" +
                "{\"type\":\"and\",\"filters\":[" +
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"A\"}," +
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"B\"}]}," +
                "{\"type\":\"equal\",\"field\":\"tags\",\"value\":\"C\"}]}",
                json);
        }

        [TestMethod]
        public void Ampersand_IsLiteralTagChar()
        {
            // 実在タグ Akira,Jeans&Chinpo を1語として検索できること（Issue #55 の主目的）
            bool ok = TagConditionParser.TryParse("Akira,Jeans&Chinpo", out string json, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"Akira,Jeans&Chinpo\"}",
                json);
        }

        [TestMethod]
        public void AmpersandOnly_IsLiteralTagChar()
        {
            Assert.IsTrue(TagConditionParser.TryParse("&", out string json1, out _));
            Assert.AreEqual("{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"&\"}", json1);

            Assert.IsTrue(TagConditionParser.TryParse("&&", out string json2, out _));
            Assert.AreEqual("{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"&&\"}", json2);

            // 全角の＆は従来どおりタグの文字（区切りにはしない）
            Assert.IsTrue(TagConditionParser.TryParse("＆", out string json3, out _));
            Assert.AreEqual("{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"＆\"}", json3);
        }

        [TestMethod]
        public void StarInsideTerm_IsLiteral()
        {
            // 実在タグ DECO*27 は途中の * を文字として完全一致検索できること
            bool ok = TagConditionParser.TryParse("DECO*27", out string json, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"DECO*27\"}",
                json);
        }

        [TestMethod]
        public void OnlyTrailingStar_IsPartialMatch()
        {
            Assert.IsTrue(TagConditionParser.TryParse("A*B*", out string json1, out _));
            Assert.AreEqual("{\"type\":\"equal\",\"field\":\"tags\",\"value\":\"A*B\"}", json1);

            // 末尾の * だけを外す。残った * はタグの文字
            Assert.IsTrue(TagConditionParser.TryParse("A**", out string json2, out _));
            Assert.AreEqual("{\"type\":\"equal\",\"field\":\"tags\",\"value\":\"A*\"}", json2);
        }

        [TestMethod]
        public void FullWidthSpace_IsAnd()
        {
            bool ok = TagConditionParser.TryParse("A　B", out string json, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(
                "{\"type\":\"and\",\"filters\":[" +
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"A\"}," +
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"B\"}]}",
                json);
        }

        [TestMethod]
        public void MultipleWhitespace_IsSingleSeparator()
        {
            bool ok = TagConditionParser.TryParse("  A  \t  B  ", out string json, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(
                "{\"type\":\"and\",\"filters\":[" +
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"A\"}," +
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"B\"}]}",
                json);
        }

        [TestMethod]
        public void ThreeTerms_AreAnd()
        {
            bool ok = TagConditionParser.TryParse("A B C", out string json, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(
                "{\"type\":\"and\",\"filters\":[" +
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"A\"}," +
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"B\"}," +
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"C\"}]}",
                json);
        }

        [TestMethod]
        public void OrWithSpaces_IsAllowed()
        {
            bool ok = TagConditionParser.TryParse(" A | B ", out string json, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(
                "{\"type\":\"or\",\"filters\":[" +
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"A\"}," +
                "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"B\"}]}",
                json);
        }

        [TestMethod]
        public void EmptyCondition_Fails()
        {
            Assert.IsFalse(TagConditionParser.TryParse("", out string json, out string error));
            Assert.IsNull(json);
            Assert.IsFalse(string.IsNullOrEmpty(error));

            Assert.IsFalse(TagConditionParser.TryParse("   ", out json, out error));
            Assert.IsNull(json);
        }

        [TestMethod]
        public void EmptyOrGroup_Fails()
        {
            // 空グループを空のANDフィルタとして通すと、意図しない条件で検索してしまうため必ずエラーにする
            Assert.IsFalse(TagConditionParser.TryParse("A|", out _, out string error1));
            Assert.IsFalse(string.IsNullOrEmpty(error1));

            Assert.IsFalse(TagConditionParser.TryParse("|A", out _, out string error2));
            Assert.IsFalse(string.IsNullOrEmpty(error2));

            Assert.IsFalse(TagConditionParser.TryParse("A||B", out _, out string error3));
            Assert.IsFalse(string.IsNullOrEmpty(error3));

            Assert.IsFalse(TagConditionParser.TryParse("A | ", out _, out string error4));
            Assert.IsFalse(string.IsNullOrEmpty(error4));
        }

        [TestMethod]
        public void StarOnly_Fails()
        {
            Assert.IsFalse(TagConditionParser.TryParse("*", out _, out string error1));
            StringAssert.Contains(error1, "*");

            // 空白がAND区切りになったため、旧仕様の「A *」はAと単独*の2語になりエラーになる（正しくは A*）
            Assert.IsFalse(TagConditionParser.TryParse("A *", out _, out string error2));
            StringAssert.Contains(error2, "*");

            Assert.IsFalse(TagConditionParser.TryParse("* A", out _, out string error3));
            StringAssert.Contains(error3, "*");
        }
    }
}
