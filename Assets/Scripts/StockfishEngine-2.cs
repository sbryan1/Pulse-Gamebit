using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

// Talks to the Stockfish executable over UCI.
// Besides finding moves, it can list every legal move in a position
// ("go perft 1") and report whether the side to move is in check ("d"),
// so the game never needs its own rules code.
//
// On Android the engine is launched through Java (java.lang.ProcessBuilder),
// because C#'s Process.Start doesn't work in Android builds.
public class StockfishEngine : MonoBehaviour
{
    Process proc;
    readonly ConcurrentQueue<string> lines = new();

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject jProc, jWriter, jReader;
#endif

    public System.Action<string> OnBestMove;          // "e2e4", "e7e8q" or "(none)"
    public System.Action<List<string>> OnLegalMoves;  // every legal move in the current position
    public System.Action<bool> OnCheckStatus;         // true if the side to move is in check

    List<string> perft; // collects "e2e4: 1" lines while a legal-move request is running
    static readonly Regex PerftLine = new(@"^([a-h][1-8][a-h][1-8][qrbn]?):\s*\d+$");

    public void StartEngine(string path, int skillLevel = 10)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        StartAndroid(path);
#else
        StartDesktop(path);
#endif
        Send("uci");
        Send("setoption name Skill Level value " + skillLevel);
        Send("isready");
    }

    void StartDesktop(string path)
    {
        proc = new Process();
        proc.StartInfo.FileName = path;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardInput = true;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.CreateNoWindow = true;
        proc.Start();

        new Thread(() => {
            string line;
            while ((line = proc.StandardOutput.ReadLine()) != null)
                lines.Enqueue(line);
        }) { IsBackground = true }.Start();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void StartAndroid(string path)
    {
        // new ProcessBuilder(path).redirectErrorStream(true).start()
        using (var builder = new AndroidJavaObject("java.lang.ProcessBuilder", (object)new[] { path }))
        {
            builder.Call<AndroidJavaObject>("redirectErrorStream", true)?.Dispose();
            jProc = builder.Call<AndroidJavaObject>("start");
        }

        var output = jProc.Call<AndroidJavaObject>("getInputStream");
        var input  = jProc.Call<AndroidJavaObject>("getOutputStream");
        jReader = new AndroidJavaObject("java.io.BufferedReader",
                      new AndroidJavaObject("java.io.InputStreamReader", output));
        jWriter = new AndroidJavaObject("java.io.PrintWriter", input);

        var reader = jReader;
        new Thread(() => {
            // A background thread must be attached to Java before it can call Java objects.
            AndroidJNI.AttachCurrentThread();
            try
            {
                while (true)
                {
                    string line = reader.Call<string>("readLine");
                    if (line == null) break; // engine closed
                    lines.Enqueue(line);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Stockfish output stopped: " + e.Message);
            }
            finally
            {
                AndroidJNI.DetachCurrentThread();
            }
        }) { IsBackground = true }.Start();
    }
#endif

    public void Send(string cmd)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (jWriter == null) return;
        jWriter.Call("println", cmd);
        jWriter.Call("flush");
#else
        if (proc == null) return;
        proc.StandardInput.WriteLine(cmd);
        proc.StandardInput.Flush();
#endif
    }

    public void RequestMove(string fen, int moveTimeMs = 1000)
    {
        Send("position fen " + fen);
        Send("go movetime " + moveTimeMs);
    }

    // positionCmd is e.g. "position startpos moves e2e4 e7e5"
    public void RequestLegalMoves(string positionCmd)
    {
        perft = new List<string>();
        Send(positionCmd);
        Send("go perft 1");
    }

    // Uses the position from the last command sent.
    public void RequestCheckStatus() => Send("d");

    void Update()
    {
        while (lines.TryDequeue(out var raw))
        {
            string line = raw.Trim();

            if (line.StartsWith("bestmove"))
            {
                OnBestMove?.Invoke(line.Split(' ')[1]);
                continue;
            }

            if (perft != null)
            {
                var m = PerftLine.Match(line);
                if (m.Success) { perft.Add(m.Groups[1].Value); continue; }

                if (line.StartsWith("Nodes searched"))
                {
                    var result = perft;
                    perft = null;
                    OnLegalMoves?.Invoke(result);
                    continue;
                }
            }

            // "d" prints e.g. "Checkers: e8" when in check, or just "Checkers:" when not
            if (line.StartsWith("Checkers:"))
                OnCheckStatus?.Invoke(line.Length > "Checkers:".Length);
        }
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (jProc != null)
        {
            try { Send("quit"); jProc.Call("destroy"); } catch { }
            jProc = null;
            jWriter = null;
        }
#else
        if (proc != null && !proc.HasExited) { Send("quit"); proc.Kill(); }
#endif
    }
}
