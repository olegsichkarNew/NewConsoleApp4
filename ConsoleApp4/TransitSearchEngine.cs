using System;
using System.Collections.Generic;

namespace AstroSwissEph
{
    /// <summary>
    /// Scans time grid, collects hits, merges them into periods.
    /// Works in UTC only. All domain-specific matching lives in IEventCondition.
    /// </summary>
    public sealed class TransitSearchEngine
    {
        private readonly IEphemeris _eph;
        private readonly IEventCondition _condition;

        public TransitSearchEngine(IEphemeris eph, IEventCondition condition)
        {
            _eph = eph ?? throw new ArgumentNullException(nameof(eph));
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public IReadOnlyList<Period> FindPeriods(SearchRequest req)
        {
            if (req.EndUtc <= req.StartUtc) throw new ArgumentException("EndUtc must be > StartUtc");
            if (req.Step <= TimeSpan.Zero) throw new ArgumentException("Step must be positive");
            if (req.Bodies is null || req.Bodies.Count == 0) throw new ArgumentException("Bodies must be non-empty");
            if (req.StartUtc.Kind != DateTimeKind.Utc || req.EndUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("StartUtc/EndUtc must be UTC");

            var maxGap = req.MaxGapToMerge ?? TimeSpan.FromTicks(req.Step.Ticks * 2);

            var hits = new List<Hit>();

            for (var t = req.StartUtc; t <= req.EndUtc; t = t.Add(req.Step))
            {
                var states = _eph.GetStates(t, req.Bodies);
                if (_condition.IsMatch(states, req))
                    hits.Add(new Hit(t, states));
            }

            return MergeHitsIntoPeriods(hits, maxGap);
        }

        private static IReadOnlyList<Period> MergeHitsIntoPeriods(List<Hit> hits, TimeSpan maxGap)
        {
            if (hits.Count == 0) return Array.Empty<Period>();
            hits.Sort((a, b) => a.TimeUtc.CompareTo(b.TimeUtc));

            var periods = new List<Period>();
            var current = new List<Hit> { hits[0] };

            var start = hits[0].TimeUtc;
            var end = hits[0].TimeUtc;

            for (int i = 1; i < hits.Count; i++)
            {
                var gap = hits[i].TimeUtc - end;

                if (gap <= maxGap)
                {
                    current.Add(hits[i]);
                    end = hits[i].TimeUtc;
                }
                else
                {
                    periods.Add(new Period(start, end, current.ToArray()));
                    current = new List<Hit> { hits[i] };
                    start = end = hits[i].TimeUtc;
                }
            }

            periods.Add(new Period(start, end, current.ToArray()));
            return periods;
        }
    }
}
