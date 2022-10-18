using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace GrepTool
{
    public class GrepProcess : IDisposable
    {
        private readonly ProcessStartInfo _startInfo = new ProcessStartInfo
        {
            FileName = "git",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };

        private readonly Process _process;
        private readonly Action<string> _onLineReceived;
        private readonly int _parentId;
        private readonly string _search;
        private int _progressId;

        public GrepProcess(string search, Action<string> onLineReceived = null, int progressIdParent = -1)
        {
            _startInfo.Arguments = "grep " + search;
            _search = search;
            _onLineReceived = onLineReceived;
            _process = new Process();
            _process.StartInfo = _startInfo;
            _process.EnableRaisingEvents = true;
            _process.OutputDataReceived += OutputReceived;
            _process.ErrorDataReceived += ErrorReceived;
            _parentId = progressIdParent;
        }

        public Task<int> StartAsync()
        {
            var tcs = new TaskCompletionSource<int>();
            _process.Exited += (sender, args) =>
            {
                Progress.Finish(_progressId,
                    _process.ExitCode == 0 ? Progress.Status.Succeeded : Progress.Status.Failed);
                tcs.SetResult(_process.ExitCode);
            };
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            _progressId = Progress.Start("Grep Search", "Searching for " + _search, parentId: _parentId);
            Progress.RegisterCancelCallback(_progressId, CancelProcess);
            return tcs.Task;
        }

        private void OutputReceived(object sender, DataReceivedEventArgs e)
        {
            _onLineReceived?.Invoke(e.Data);
        }

        private void ErrorReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Debug.LogWarning(e.Data);
                Progress.Finish(_progressId, Progress.Status.Failed);
            }
        }

        private bool CancelProcess()
        {
            _process?.Kill();
            return _process?.HasExited ?? true;
        }

        public void Dispose()
        {
            _process?.Dispose();
        }
    }
}
