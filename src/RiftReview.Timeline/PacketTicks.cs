using System.Globalization;
using RiftReview.Core.Matches;

namespace RiftReview.Timeline;

/// <summary>One packet sent to a champion: its type and the champion's index.</summary>
internal readonly record struct ChampionPacket(ushort Type, int Hero);

/// <summary>One packet of a unit that is not a champion: when, and its type.</summary>
internal readonly record struct UnitPacket(float Time, ushort Type);

/// <summary>
/// The replay's packets as the credit decoders need them (docs/research/rofl2-payload.md, reward
/// bursts): champion packets grouped by tick (one game time) in the order the file has them, and
/// every other unit's packets. When a unit dies the server pays the champions it credits in that
/// tick, in a fixed order; only types, times, order and addressees are read, never contents.
/// </summary>
internal sealed class PacketTicks
{
    private PacketTicks(float[] times, ChampionPacket[][] ticks, Dictionary<uint, List<UnitPacket>> units, Team[] teams)
    {
        Times = times;
        Ticks = ticks;
        Units = units;
        Teams = teams;
        index = BuildIndex(ticks);
    }

    /// <summary>Tick times, ascending.</summary>
    public float[] Times { get; }

    /// <summary>The champion packets of each tick, in file order.</summary>
    public ChampionPacket[][] Ticks { get; }

    /// <summary>Packets of every other unit (monsters, structures, minions), in time order.</summary>
    public Dictionary<uint, List<UnitPacket>> Units { get; }

    /// <summary>Each champion's team, by index.</summary>
    public Team[] Teams { get; }

    /// <summary>Reads the chunks once.</summary>
    public static PacketTicks Read(IReadOnlyList<GameDataRecord> chunks, IReadOnlyList<uint> heroIds, IReadOnlyList<PlayerStats> players)
    {
        var first = heroIds[0];
        var champion = new List<(float Time, ChampionPacket Packet)>();
        var units = new Dictionary<uint, List<UnitPacket>>();
        foreach (var chunk in chunks)
        {
            var reader = new BlockReader(chunk.Data);
            while (reader.TryRead(out var block))
            {
                var hero = block.Parameter - first;
                if (block.Parameter >= first && hero < heroIds.Count)
                {
                    champion.Add((block.Time, new ChampionPacket(block.Type, (int)hero)));
                }
                else if (block.Parameter != 0)
                {
                    if (!units.TryGetValue(block.Parameter, out var list))
                    {
                        list = [];
                        units[block.Parameter] = list;
                    }

                    list.Add(new UnitPacket(block.Time, block.Type));
                }
            }
        }

        // OrderBy is stable: within a tick the file's order stays, and that order carries the credit.
        var groups = champion.OrderBy(c => c.Time).GroupBy(c => c.Time).ToList();
        foreach (var list in units.Values)
        {
            list.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        return new PacketTicks(
            [.. groups.Select(g => g.Key)],
            [.. groups.Select(g => g.Select(c => c.Packet).ToArray())],
            units,
            [.. players.Select(p => p.Team)]);
    }

    /// <summary>The champion packets of the tick at exactly <paramref name="time"/>, empty when there is none.</summary>
    public ChampionPacket[] At(double time)
    {
        var index = Array.BinarySearch(Times, (float)time);
        if (index < 0)
        {
            index = ~index;
            if (index > 0 && (index >= Times.Length || Math.Abs(Times[index - 1] - time) < Math.Abs(Times[index] - time)))
            {
                index--;
            }
        }

        return index < Times.Length && Math.Abs(Times[index] - time) < 0.02 ? Ticks[index] : [];
    }

    /// <summary>The distinct packet types champions receive.</summary>
    public IReadOnlyList<ushort> ChampionTypes() => [.. Index.Keys];

    /// <summary>How many packets of a type champions receive.</summary>
    public int Count(ushort type) => Index.TryGetValue(type, out var entry) ? entry.Packets : 0;

    /// <summary>The ticks (indexes into <see cref="Ticks"/>) that hold a packet of a type.</summary>
    public IReadOnlyList<int> TicksWith(ushort type) => Index.TryGetValue(type, out var entry) ? entry.Ticks : [];

    private Dictionary<ushort, (int Packets, List<int> Ticks)> Index => index;

    private readonly Dictionary<ushort, (int Packets, List<int> Ticks)> index;

    private static Dictionary<ushort, (int Packets, List<int> Ticks)> BuildIndex(ChampionPacket[][] ticks)
    {
        var built = new Dictionary<ushort, (int Packets, List<int> Ticks)>();
        for (var i = 0; i < ticks.Length; i++)
        {
            foreach (var packet in ticks[i])
            {
                var entry = built.TryGetValue(packet.Type, out var e) ? e : (Packets: 0, Ticks: new List<int>());
                if (entry.Ticks.Count == 0 || entry.Ticks[^1] != i)
                {
                    entry.Ticks.Add(i);
                }

                built[packet.Type] = (entry.Packets + 1, entry.Ticks);
            }
        }

        return built;
    }

    /// <summary>A per-player end-of-game stat (0 when missing).</summary>
    public static int[] Stat(IReadOnlyList<PlayerStats> players, params string[] keys) =>
        [.. players.Select(p => keys.Sum(k => int.TryParse(p.RawStats.GetValueOrDefault(k), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0))];

    /// <summary>How far per-player counts are from a stat: the sum of the differences.</summary>
    public static int Distance(int[] counts, int[] stat) => counts.Zip(stat, (a, b) => Math.Abs(a - b)).Sum();
}
