using ConsoleApp4;
using System;
using System.Collections.Generic;

namespace AstroSwissEph
{
    public interface IEphemeris
    {
        IReadOnlyDictionary<SweBody, BodyState> GetStates(DateTime timeUtc, IReadOnlyList<SweBody> bodies);
    }

    public interface IEventCondition
    {
        bool IsMatch(IReadOnlyDictionary<SweBody, BodyState> states, SearchRequest req);
    }

    public sealed record SearchRequest(
        DateTime StartUtc,
        DateTime EndUtc,
        TimeSpan Step,
        double DiffDeg,
        IReadOnlyList<SweBody> Bodies,
        TimeSpan? MaxGapToMerge = null
    );
}
