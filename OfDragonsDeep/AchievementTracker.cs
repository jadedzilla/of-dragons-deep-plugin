using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using LuminaAchievement = Lumina.Excel.Sheets.Achievement;

namespace OfDragonsDeep;

public sealed class AchievementEntry(uint id, string name, string description, uint points)
{
    public uint Id { get; } = id;
    public string Name { get; } = name;
    public string Description { get; } = description;
    public uint Points { get; } = points;
    public bool Complete { get; set; }

    /// <summary>Server-reported progress; null when not requested or the server didn't answer.</summary>
    public uint? Current { get; set; }
    public uint? Max { get; set; }
}

/// <summary>
/// Loads completion (and optionally progress) for the "Fisher" achievement category.
/// Completion comes from the game's achievement bitmap; progress has to be asked from the server
/// one achievement at a time, so it is done in the background on the framework thread.
/// </summary>
public sealed class AchievementTracker : IDisposable
{
    /// <summary>AchievementCategory row "Fisher" (under "Crafting &amp; Gathering"), includes ocean fishing.</summary>
    public const uint FisherCategoryId = 34;

    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ProgressTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProgressSpacing = TimeSpan.FromMilliseconds(150);
    /// <summary>Progress fetched from the server is reused for this long, so repeated exports are instant.</summary>
    private static readonly TimeSpan ProgressMaxAge = TimeSpan.FromMinutes(10);

    private readonly Dictionary<uint, (uint Current, uint Max)> progressCache = [];
    private DateTime progressFetchedAt = DateTime.MinValue;

    private enum Phase { Idle, LoadingCompletion, LoadingProgress }

    private Phase phase = Phase.Idle;
    private bool withProgress;
    private bool requestedCompletion;
    private DateTime phaseStarted;

    private readonly Queue<AchievementEntry> progressQueue = new();
    private AchievementEntry? waitingFor;
    private DateTime requestedAt;
    private DateTime nextRequestAt;

    public List<AchievementEntry> Entries { get; private set; } = [];
    public bool IsBusy => phase != Phase.Idle;
    public int ProgressRemaining => progressQueue.Count + (waitingFor != null ? 1 : 0);
    public string? Error { get; private set; }

    /// <summary>Increments whenever Entries change, so the UI knows to rebuild its export text.</summary>
    public int Revision { get; private set; }

    public AchievementTracker()
    {
        Plugin.Framework.Update += OnUpdate;
    }

    public void Dispose() => Plugin.Framework.Update -= OnUpdate;

    public void Start(bool includeProgress)
    {
        if (IsBusy)
            return;
        withProgress = includeProgress;
        requestedCompletion = false;
        Error = null;
        progressQueue.Clear();
        waitingFor = null;
        phase = Phase.LoadingCompletion;
        phaseStarted = DateTime.UtcNow;
    }

    public void Reset()
    {
        progressCache.Clear();
        progressFetchedAt = DateTime.MinValue;
        phase = Phase.Idle;
        progressQueue.Clear();
        waitingFor = null;
        Error = null;
        if (Entries.Count > 0)
        {
            Entries = [];
            Revision++;
        }
    }

    private unsafe void OnUpdate(IFramework framework)
    {
        if (phase == Phase.Idle)
            return;

        if (!Plugin.ClientState.IsLoggedIn)
        {
            Reset();
            return;
        }

        var ach = Achievement.Instance();
        if (ach == null)
        {
            Fail("Achievement data isn't available.");
            return;
        }

        var now = DateTime.UtcNow;

        if (phase == Phase.LoadingCompletion)
        {
            if (!ach->IsLoaded())
            {
                // Same request the game makes when the Achievements window is opened.
                if (!requestedCompletion && ach->State == Achievement.AchievementState.Invalid)
                {
                    ach->RequestCompletedAchievements();
                    requestedCompletion = true;
                }

                if (now - phaseStarted > LoadTimeout)
                    Fail("The server didn't send achievement data. Try opening the Achievements window, then try again.");
                return;
            }

            Entries = BuildEntries(ach);
            Revision++;

            if (withProgress && now - progressFetchedAt < ProgressMaxAge)
            {
                foreach (var e in Entries.Where(e => !e.Complete))
                {
                    if (progressCache.TryGetValue(e.Id, out var p))
                        (e.Current, e.Max) = (p.Current, p.Max);
                }
                phase = Phase.Idle;
            }
            else if (withProgress)
            {
                progressCache.Clear();
                foreach (var e in Entries.Where(e => !e.Complete))
                    progressQueue.Enqueue(e);
                phase = Phase.LoadingProgress;
                nextRequestAt = now;
            }
            else
            {
                phase = Phase.Idle;
            }
            return;
        }

        // Phase.LoadingProgress
        if (waitingFor != null)
        {
            if (ach->ProgressAchievementId == waitingFor.Id && ach->ProgressRequestState == Achievement.AchievementState.Loaded)
            {
                waitingFor.Current = ach->ProgressCurrent;
                waitingFor.Max = ach->ProgressMax;
                progressCache[waitingFor.Id] = (ach->ProgressCurrent, ach->ProgressMax);
                waitingFor = null;
                nextRequestAt = now + ProgressSpacing;
                Revision++;
            }
            else if (now - requestedAt > ProgressTimeout)
            {
                waitingFor = null; // leave progress unknown
                nextRequestAt = now + ProgressSpacing;
            }
            return;
        }

        if (progressQueue.Count == 0)
        {
            progressFetchedAt = now;
            phase = Phase.Idle;
            return;
        }

        if (now < nextRequestAt)
            return;

        waitingFor = progressQueue.Dequeue();
        requestedAt = now;
        ach->RequestAchievementProgress(waitingFor.Id);
    }

    private static unsafe List<AchievementEntry> BuildEntries(Achievement* ach)
    {
        var list = new List<AchievementEntry>();
        foreach (var row in Plugin.DataManager.GetExcelSheet<LuminaAchievement>())
        {
            if (row.AchievementCategory.RowId != FisherCategoryId)
                continue;

            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            list.Add(new AchievementEntry(row.RowId, name, row.Description.ExtractText(), row.Points)
            {
                Complete = ach->IsComplete((int)row.RowId),
            });
        }
        return list;
    }

    private void Fail(string message)
    {
        Error = message;
        phase = Phase.Idle;
        progressQueue.Clear();
        waitingFor = null;
    }
}
