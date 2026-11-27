using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.LiveData;

namespace MyPoGoCompanion.Jobs.Research;

internal sealed record Observation(ResearchTopic Topic, string Key, ResearchFact Fact, Uri Url, DateTimeOffset At);

internal sealed class ResearchPublication(AppDbContext db)
{
    public int Added { get; private set; }
    public int Changed { get; private set; }
    public int Flagged { get; private set; }

    public async Task ApplyAsync(Observation item, CancellationToken token)
    {
        var f = item.Fact;
        switch (item.Topic)
        {
            case ResearchTopic.Raids:
                await Upsert(db.RaidBosses, new RaidBoss { Key = item.Key, FormId = f.FormId! }, item, boss =>
                {
                    boss.FormId = f.FormId!;
                    boss.Tier = Enum.Parse<RaidTier>(f.Kind!);
                    boss.ShinyAvailable = f.Shiny;
                    boss.IsShadow = f.Shadow;
                    SetSchedule(boss, f);
                }, token);
                break;
            case ResearchTopic.MaxBattles:
                await Upsert(db.MaxBosses, new MaxBoss { Key = item.Key, FormId = f.FormId! }, item, boss =>
                {
                    boss.FormId = f.FormId!;
                    boss.Stars = f.Stars!.Value;
                    boss.ShinyAvailable = f.Shiny;
                    boss.IsGigantamax = f.Gigantamax;
                    SetSchedule(boss, f);
                }, token);
                break;
            case ResearchTopic.Eggs:
                await Upsert(db.EggPoolEntries, new EggPoolEntry { Key = item.Key, FormId = f.FormId! }, item, egg =>
                {
                    egg.FormId = f.FormId!;
                    egg.Egg = Enum.Parse<EggKind>(f.Kind!);
                    egg.Rarity = f.Rarity;
                    egg.ShinyAvailable = f.Shiny;
                }, token);
                break;
            case ResearchTopic.Events:
                await Upsert(db.Events.Include(e => e.Pokemon), new GameEvent { Key = item.Key, NameEn = f.Name! }, item, ev =>
                {
                    ev.NameEn = f.Name!;
                    ev.SummaryEn = f.Summary;
                    ev.Kind = Enum.Parse<EventKind>(f.Kind!);
                    SetSchedule(ev, f);
                    ev.Pokemon.Clear();
                    ev.Pokemon.AddRange(f.Encounters.Select((e, i) => new EventPokemon { FormId = e.FormId, Shiny = e.Shiny, SortOrder = i }));
                }, token);
                break;
            case ResearchTopic.FieldResearch:
                await Upsert(db.ResearchTasks.Include(t => t.Rewards), new ResearchTask { Key = item.Key, TaskEn = f.Name! }, item, task =>
                {
                    task.TaskEn = f.Name!;
                    task.Rewards.Clear();
                    task.Rewards.AddRange(f.Encounters.Select(e => new ResearchReward { FormId = e.FormId, ShinyAvailable = e.Shiny }));
                }, token);
                break;
        }
    }

    private static void SetSchedule(IScheduled target, ResearchFact f)
    {
        target.StartsAt = f.Start!.Value;
        target.EndsAt = f.End;
        target.TimeZone = f.Zone;
    }

    private async Task Upsert<T>(IQueryable<T> query, T fresh, Observation item, Action<T> apply, CancellationToken token)
        where T : FetchedEntry
    {
        var row = await query.SingleOrDefaultAsync(e => e.Key == item.Key, token);
        var snapshot = JsonSerializer.SerializeToNode(item.Fact)!;
        if (row is null)
        {
            row = fresh;
            row.FirstFetchedAt = item.At;
            db.Add(row);
            Added++;
            apply(row);
        }
        else if (!JsonNode.DeepEquals(snapshot, JsonNode.Parse(row.SourceSnapshot ?? "null")))
        {
            if (row.FixedAt is not null || row.RemovedAt is not null)
            {
                row.SourceChangedAt = item.At;
                Flagged++;
            }
            else
            {
                apply(row);
                row.ChangedAt = item.At;
                Changed++;
            }
        }

        row.FetchedAt = item.At;
        row.SourceUrl = item.Url.AbsoluteUri;
        row.SourceSnapshot = snapshot.ToJsonString();
    }
}
