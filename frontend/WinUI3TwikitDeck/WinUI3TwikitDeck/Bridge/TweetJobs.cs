using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace WinUI3TwikitDeck.Bridge
{
    /// <summary>添付メディア 1 件の進捗。</summary>
    internal sealed class MediaItemProgress(string path, string fileName)
    {
        public string Path { get; } = path;

        public string FileName { get; } = fileName;

        /// <summary>pending | waiting | uploading | processing | done | failed</summary>
        public string Phase { get; set; } = "pending";

        public int Percent { get; set; }

        public string? Error { get; set; }

        public JsonObject ToJson() => new()
        {
            ["path"] = Path,
            ["file_name"] = FileName,
            ["phase"] = Phase,
            ["percent"] = Percent,
            ["error"] = Error,
        };
    }

    /// <summary>投稿ジョブ（新規・返信・引用）。</summary>
    internal sealed class TweetJob(string jobId, string text, List<MediaItemProgress> items, string? replyTo, string? quoteTweetId)
    {
        public string JobId { get; } = jobId;

        public string Text { get; } = text;

        public List<MediaItemProgress> Items { get; } = items;

        /// <summary>返信先ツイート ID（返信でなければ null）。</summary>
        public string? ReplyTo { get; } = replyTo;

        /// <summary>引用元ツイート ID（引用でなければ null）。</summary>
        public string? QuoteTweetId { get; } = quoteTweetId;

        /// <summary>queued | running | succeeded | failed</summary>
        public string State { get; set; } = "queued";

        public string Message { get; set; } = string.Empty;

        public string? TweetId { get; set; }

        public DateTime CreatedAt { get; } = DateTime.UtcNow;

        public DateTime? FinishedAt { get; set; }

        public JsonObject ToJson()
        {
            var items = new JsonArray();
            foreach (var item in Items)
            {
                items.Add(item.ToJson());
            }

            return new JsonObject
            {
                ["job_id"] = JobId,
                ["state"] = State,
                ["message"] = Message,
                ["tweet_id"] = TweetId,
                ["items"] = items,
            };
        }
    }

    /// <summary>進捗通知付きのメモリ内ジョブストア（WinUI-3-twikit より移植）。</summary>
    internal sealed class TweetJobStore
    {
        public static TweetJobStore Instance { get; } = new();

        private readonly object _lock = new();
        private readonly Dictionary<string, TweetJob> _jobs = new(StringComparer.Ordinal);

        /// <summary>ジョブの状態が変わったときに jobId を通知する（呼び出しスレッドは不定）。</summary>
        public event Action<string>? JobChanged;

        public TweetJob Create(string text, IReadOnlyList<string> paths, string? replyTo = null, string? quoteTweetId = null)
        {
            var jobId = Guid.NewGuid().ToString("N");
            var items = paths
                .Select(p => new MediaItemProgress(p, string.IsNullOrEmpty(Path.GetFileName(p)) ? p : Path.GetFileName(p)))
                .ToList();
            var job = new TweetJob(jobId, text, items, replyTo, quoteTweetId);
            lock (_lock)
            {
                _jobs[jobId] = job;
                PruneUnlocked();
            }

            return job;
        }

        public TweetJob? Get(string jobId)
        {
            lock (_lock)
            {
                return _jobs.TryGetValue(jobId, out var job) ? job : null;
            }
        }

        /// <summary>ジョブの現在の状態を JSON にする（無ければ null）。</summary>
        public JsonObject? Snapshot(string jobId)
        {
            lock (_lock)
            {
                return _jobs.TryGetValue(jobId, out var job) ? job.ToJson() : null;
            }
        }

        public void UpdateItem(string jobId, int index, string? phase = null, int? percent = null, string? error = null)
        {
            lock (_lock)
            {
                if (!_jobs.TryGetValue(jobId, out var job) || index < 0 || index >= job.Items.Count)
                {
                    return;
                }

                var item = job.Items[index];
                if (phase is not null)
                {
                    item.Phase = phase;
                }

                if (percent is not null)
                {
                    item.Percent = Math.Max(0, Math.Min(100, percent.Value));
                }

                if (error is not null)
                {
                    item.Error = error;
                }
            }

            JobChanged?.Invoke(jobId);
        }

        /// <summary>まだ終わっていない添付を失敗扱いにする。</summary>
        public void FailUnfinishedItems(string jobId, string error)
        {
            lock (_lock)
            {
                if (!_jobs.TryGetValue(jobId, out var job))
                {
                    return;
                }

                foreach (var item in job.Items)
                {
                    if (item.Phase is not ("done" or "failed"))
                    {
                        item.Phase = "failed";
                        item.Error = error;
                    }
                }
            }

            JobChanged?.Invoke(jobId);
        }

        public void SetState(string jobId, string state, string message = "", string? tweetId = null)
        {
            lock (_lock)
            {
                if (!_jobs.TryGetValue(jobId, out var job))
                {
                    return;
                }

                job.State = state;
                job.Message = message;
                if (tweetId is not null)
                {
                    job.TweetId = tweetId;
                }

                if (state is "succeeded" or "failed")
                {
                    job.FinishedAt = DateTime.UtcNow;
                }
            }

            JobChanged?.Invoke(jobId);
        }

        private void PruneUnlocked(double maxAgeSec = 3600.0, int maxJobs = 50)
        {
            var now = DateTime.UtcNow;
            var stale = _jobs
                .Where(kv => kv.Value.FinishedAt is { } finished && (now - finished).TotalSeconds > maxAgeSec)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var jobId in stale)
            {
                _jobs.Remove(jobId);
            }

            if (_jobs.Count <= maxJobs)
            {
                return;
            }

            var finishedJobs = _jobs
                .Where(kv => kv.Value.FinishedAt is not null)
                .OrderBy(kv => kv.Value.FinishedAt)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var jobId in finishedJobs)
            {
                if (_jobs.Count <= maxJobs)
                {
                    break;
                }

                _jobs.Remove(jobId);
            }
        }
    }
}
