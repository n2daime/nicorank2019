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
        public void AndOr_PrecedenceAndBeforeOr()
        {
            bool ok = TagConditionParser.TryParse("A&B|C*", out string json, out string error);

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
        public void Terms_AreTrimmed()
        {
            bool ok = TagConditionParser.TryParse(" A & B ", out string json, out string error);

            Assert.IsTrue(ok, error);
            Assert.IsTrue(json.Contains("\"value\":\"A\""), json);
            Assert.IsTrue(json.Contains("\"value\":\"B\""), json);
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
        public void EmptyToken_Fails()
        {
            Assert.IsFalse(TagConditionParser.TryParse("A&", out _, out string error1));
            Assert.IsFalse(string.IsNullOrEmpty(error1));

            Assert.IsFalse(TagConditionParser.TryParse("A||B", out _, out string error2));
            Assert.IsFalse(string.IsNullOrEmpty(error2));

            Assert.IsFalse(TagConditionParser.TryParse("*", out _, out string error3));
            Assert.IsFalse(string.IsNullOrEmpty(error3));
        }
    }
}
