using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using VrcScope;

namespace SCOPE
{
    [AddComponentMenu("SCOPE/Unity Logger")]
    public sealed class UnityLogger : MonoBehaviour
    {
        [SerializeField] private string logDirectoryOverride = "";
        [SerializeField] private float pollIntervalSeconds = 0.5f;
        [SerializeField] private LogType emitAs = LogType.Log;

        private Parser _parser = new Parser();

        private string _filePath;
        private long _readPosition;
        private string _pending = string.Empty;
        private Coroutine _tailCoroutine;

        private void OnEnable()
        {
            _tailCoroutine = StartCoroutine(TailLoop());
        }

        private void OnDisable()
        {
            if (_tailCoroutine != null)
            {
                StopCoroutine(_tailCoroutine);
                _tailCoroutine = null;
            }
            _filePath = null;
            _readPosition = 0;
            _pending = string.Empty;
        }

        private void OnDestroy()
        {
            _parser?.Dispose();
        }

        private IEnumerator TailLoop()
        {
            var wait = new WaitForSecondsRealtime(Mathf.Max(0.05f, pollIntervalSeconds));
            while (true)
            {
                RotateIfNewFile();
                if (_filePath != null) ReadAndProcess();
                yield return wait;
            }
        }

        // Detect VRChat creating a fresh output_log_*.txt (e.g. on restart) and
        // switch the tail to the latest file. Starts at EOF so historical
        // content from before this component was enabled is not replayed.
        private void RotateIfNewFile()
        {
            var latest = ResolveLatestLogFile();
            if (latest == _filePath) return;

            _filePath = latest;
            _pending = string.Empty;
            try
            {
                _readPosition = (_filePath != null) ? new FileInfo(_filePath).Length : 0;
            }
            catch
            {
                _readPosition = 0;
            }
        }

        private void ReadAndProcess()
        {
            string newContent;
            try
            {
                using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length < _readPosition) _readPosition = 0;
                    fs.Position = _readPosition;
                    using (var sr = new StreamReader(fs, Encoding.UTF8, true, 1024, leaveOpen: true))
                    {
                        newContent = sr.ReadToEnd();
                    }
                    _readPosition = fs.Position;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[UnityLogger] read error: " + e.Message);
                return;
            }

            if (string.IsNullOrEmpty(newContent)) return;

            _pending += newContent;
            var lastNl = _pending.LastIndexOf('\n');
            if (lastNl < 0) return;

            var complete = _pending.Substring(0, lastNl);
            _pending = _pending.Substring(lastNl + 1);

            foreach (var raw in complete.Split('\n'))
            {
                var line = raw.EndsWith("\r", StringComparison.Ordinal)
                    ? raw.Substring(0, raw.Length - 1)
                    : raw;
                if (line.Length == 0) continue;
                ProcessLine(line);
            }
        }

        private void ProcessLine(string line)
        {
            try
            {
                var jsonl = _parser.ParseLineAsJson(line);
                if (string.IsNullOrEmpty(jsonl)) return;
                Emit(jsonl);
            }
            catch (Exception e)
            {
                Debug.LogError("[UnityLogger] parser error: " + e.Message);
            }
        }

        private string ResolveLatestLogFile()
        {
            var dir = string.IsNullOrEmpty(logDirectoryOverride) ? DefaultLogDirectory() : logDirectoryOverride;
            if (!Directory.Exists(dir)) return null;
            var files = Directory.GetFiles(dir, "output_log_*.txt");
            if (files.Length == 0) return null;
            Array.Sort(files);
            return files[files.Length - 1];
        }

        private static string DefaultLogDirectory()
        {
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(user, "AppData", "LocalLow", "VRChat", "VRChat");
        }

        private void Emit(string line)
        {
            switch (emitAs)
            {
                case LogType.Warning:
                    Debug.LogWarning(line);
                    break;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    Debug.LogError(line);
                    break;
                default:
                    Debug.Log(line);
                    break;
            }
        }
    }
}
