using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace nicorank_SnapShot
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            var assemblies = new Dictionary<string, Assembly>();
            var executingAssembly = Assembly.GetExecutingAssembly();
            var resources = executingAssembly.GetManifestResourceNames().Where(n => n.EndsWith(".dll"));

            foreach (var resource in resources)
            {
                using (var stream = executingAssembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                        continue;

                    var bytes = new byte[stream.Length];
                    stream.Read(bytes, 0, bytes.Length);
                    try
                    {
                        assemblies.Add(resource, Assembly.Load(bytes));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.Print($"Failed to load: {resource}, Exception: {ex.Message}");
                    }
                }
            }

            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                var assemblyName = new AssemblyName(e.Name);

                var path = $"{assemblyName.Name}.dll";

                if (assemblies.ContainsKey(path))
                {
                    return assemblies[path];
                }

                return null;
            };


            StatusLog.SetLogWriter(new ConsolWriter());
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

//            var ctrl = new SnapController();
//            var result = ctrl.GetSnapShotAsync().Result;
        }
        public class ConsolWriter : IStatusLogWriter
        {
            void IStatusLogWriter.Write(string log)
            {
                Console.Write(log);
            }
        }

    }
}
