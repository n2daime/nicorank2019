using nicorankLib.Common;
using nicorankLib.Util.Text;

namespace UnitTest.Helpers
{
    public class TestConfigBuilder
    {
        public static void LoadFromXmlString(string xmlContent)
        {
            var config = Config.GetInstance();
            var xml = XmlSerializerUtil.Deserialize<NicoRankXml>(xmlContent);
            var field = typeof(Config).GetField("xml",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(config, xml);
        }

        public static void ResetInstance()
        {
            var field = typeof(Config).GetField("Instance",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field.SetValue(null, null);
        }
    }
}
