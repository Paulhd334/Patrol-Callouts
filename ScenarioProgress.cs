namespace PatrolsCallouts.Callouts
{
    // Three ordered interactions: a callout cannot finish before its statements
    // and evidence have been handled. Independent of the game for regression tests.
    internal sealed class ScenarioProgress
    {
        internal int Step { get; private set; }
        internal bool Advance()
        {
            if (Step < 3) Step++;
            return Step == 3;
        }
    }
}

