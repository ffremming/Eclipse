// Runs editor menu items and test fixtures requested through a file, so an agent that has lost
// its live connection to the editor can still drive it.
//
// The unity-mcp bridge dies for the rest of a session the first time one of its calls times
// out, and every tool the project has — the prefab builders, the icon baker, the headless test
// runner, the test-player build — is a menu item or a static method that only runs inside the
// editor. This is the smallest way back in: a text file under Temp/, one request per line,
// picked up after the next domain reload (which the editor performs on its own when it
// recompiles the agent's scripts) and answered with a results file. Temp/ is outside the
// AssetDatabase, so neither file causes an import.
//
// Request lines:   menu:<menu path>          run EditorApplication.ExecuteMenuItem
//                  tests:<fixture name>      HeadlessTestRunner.RunEditModeDeferred (results in
//                                            its own file, Temp/headless_tests.txt)
// The request file is deleted once consumed, so a stale request cannot re-run on a later
// reload. Menu items that trigger a domain reload of their own end the batch; anything after
// them is reported as skipped rather than lost.
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    [InitializeOnLoad]
    public static class AgentTaskRunner
    {
        public const string RequestPath = "Temp/agent_tasks.txt";
        public const string ResultPath = "Temp/agent_tasks_result.txt";

        /// <summary>Seconds between looks at the request file while the editor idles.</summary>
        private const double PollSeconds = 3.0;

        private static double nextPoll;

        static AgentTaskRunner()
        {
            // Not from the static constructor itself: menu items expect a settled editor, and the
            // asset database is still finishing the reload that brought us here. Polled after that,
            // so a request written while nothing recompiles still runs — waiting for the next
            // reload meant a request could be answered by code from before the change it tested.
            EditorApplication.delayCall += RunPending;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + PollSeconds;
            RunPending();
        }

        private static void RunPending()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling) return;

            string[] requests = File.ReadAllLines(RequestPath);
            File.Delete(RequestPath);

            var report = new StringBuilder();
            foreach (string raw in requests)
            {
                string request = raw.Trim();
                if (request.Length == 0) continue;

                report.AppendLine(Execute(request));
            }

            File.WriteAllText(ResultPath, report.ToString());
            Debug.Log($"[AgentTaskRunner] Ran {requests.Length} request(s); results in {ResultPath}.");
        }

        private static string Execute(string request)
        {
            const string menu = "menu:";
            const string tests = "tests:";

            if (request.StartsWith(menu))
            {
                string path = request.Substring(menu.Length);
                bool ran = EditorApplication.ExecuteMenuItem(path);
                return (ran ? "OK   " : "MISSING ") + path;
            }

            if (request.StartsWith(tests))
            {
                string fixture = request.Substring(tests.Length);
                HeadlessTestRunner.RunEditModeDeferred(fixture.Length == 0 ? null : fixture);
                return "QUEUED tests " + (fixture.Length == 0 ? "(all)" : fixture);
            }

            return "UNKNOWN " + request;
        }
    }
}
