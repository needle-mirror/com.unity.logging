using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LoggingCommon
{
    public static class DebuggerUtils
    {
        private static void LaunchHelper(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            var processTemp = new Process {StartInfo = startInfo, EnableRaisingEvents = true};
            try
            {
                processTemp.Start();
            }
            catch
            {
                throw;
            }

            processTemp.WaitForExit();
        }

        public static void Launch(string optionalExplanation = "")
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Debugger.Launch();
            }
            else
            {
                string text = $"Attach to {Process.GetCurrentProcess().Id}. {optionalExplanation}";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    LaunchHelper("/usr/bin/osascript", $"-e \"display dialog \\\"{text}\\\" with icon note buttons {{\\\"OK\\\"}}\"");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    LaunchHelper("/usr/bin/zenity", $@"--info --title=""Attach Debugger"" --text=""{text}"" --no-wrap");
                }
                else
                {
                    // throw?
                }
            }
        }
    }
}
